using Iterasa.ResumeAnalyzer.Domain.Common;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;

namespace Iterasa.ResumeAnalyzer.Domain.Vacancies;

/// <summary>
/// Vacante de una empresa: el puesto, sus requisitos y su ciclo de vida.
/// </summary>
/// <remarks>
/// <para>Raíz del agregado; los requisitos solo cambian a través de ella. Cambiar el texto de
/// un requisito cuenta como requisito nuevo: sus resultados se descartan y se vuelve a
/// evaluar. Cambiar tipo o peso solo recalcula.</para>
/// <para>Estados: abierta ⇄ en pausa, y cualquiera de las dos → cerrada → abierta. En pausa no
/// recibe CV, no avisa a nadie y no adelanta la retención.</para>
/// <para>Retención: los CV se borran a los <c>retentionDays</c> del cierre. Mientras sigue
/// abierta o en pausa cuentan desde la fecha prevista de cierre: nada se guarda para siempre.</para>
/// </remarks>
internal sealed class Vacancy : AuditableEntity, ITenantOwned {

    public const int MaxTitleLength = 120;
    public const int MaxRequirements = 30;
    public const int MaxApplyTokenLength = 64;

    /// <summary>Área por defecto: el alta todavía no la pide.</summary>
    public const string DefaultArea = "General";

    /// <summary>Lo más lejos que puede quedar la fecha prevista de cierre.</summary>
    public const int MaxDaysUntilClose = 366;

    private readonly List<Requirement> requirements = [];

    private Vacancy() {
    }

    /// <summary>Identificador.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Puesto.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Área.</summary>
    public string Area { get; private set; } = DefaultArea;

    /// <summary>Modalidad.</summary>
    public Modality Modality { get; private set; }

    /// <summary>Prioridad.</summary>
    public Priority Priority { get; private set; } = Priority.Medium;

    /// <summary>Estado.</summary>
    public VacancyStatus Status { get; private set; }

    /// <summary>Motivo del cierre, si está cerrada.</summary>
    public CloseReason? CloseReason { get; private set; }

    /// <summary>Responsable.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Fecha prevista de cierre.</summary>
    public DateOnly ClosesOn { get; private set; }

    /// <summary>Día en que se abrió (o se reabrió).</summary>
    public DateOnly OpenedOn { get; private set; }

    /// <summary>Día en que se cerró, si está cerrada.</summary>
    public DateOnly? ClosedOn { get; private set; }

    /// <summary>Token del formulario público de postulación.</summary>
    public string ApplyToken { get; private set; } = string.Empty;

    /// <summary>Requisitos, en orden.</summary>
    public IReadOnlyList<Requirement> Requirements => requirements;

    /// <summary>Crea una vacante abierta.</summary>
    /// <exception cref="DomainException">Datos o requisitos no válidos.</exception>
    public static Vacancy Create(VacancyCreationArgs args, DateTimeOffset now) {

        if (args.Id == Guid.Empty || args.TenantId == Guid.Empty) {
            throw new DomainException("Vacancy ids cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(args.ApplyToken) || args.ApplyToken.Length > MaxApplyTokenLength) {
            throw new DomainException("ApplyToken is invalid.");
        }

        if (args.Requirements.Any(draft => draft.Id is not null)) {
            throw new DomainException("A new vacancy cannot reference existing requirements.");
        }

        var vacancy = new Vacancy {
            Id = args.Id,
            TenantId = args.TenantId,
            Status = VacancyStatus.Open,
            OpenedOn = Today(now),
            ApplyToken = args.ApplyToken,
        };

        vacancy.ApplyDetails(args.Details, now, isNew: true);
        vacancy.ApplyRequirements(args.Requirements);
        vacancy.SetCreated(args.ActorId, now);

        return vacancy;

    }

    // --- Edición -------------------------------------------------------------------

    /// <summary>
    /// Cambia los datos y los requisitos. Los pesos de los requisitos que se conservan no cambian.
    /// </summary>
    /// <returns>Requisitos que hay que volver a evaluar: los nuevos y los que cambiaron de texto.</returns>
    public IReadOnlyList<Guid> Update(VacancyDetails details, IReadOnlyList<RequirementDraft> drafts, Guid actorId, DateTimeOffset now) {
        ApplyDetails(details, now, isNew: false);
        var changed = ApplyRequirements(drafts);
        SetUpdated(actorId, now);
        return changed;
    }

    /// <summary>Sustituye los requisitos, con sus pesos.</summary>
    /// <returns>Requisitos que hay que volver a evaluar.</returns>
    public IReadOnlyList<Guid> ReplaceRequirements(IReadOnlyList<RequirementDraft> drafts, Guid actorId, DateTimeOffset now) {
        var changed = ApplyRequirements(drafts);
        SetUpdated(actorId, now);
        return changed;
    }

    // --- Ciclo de vida -------------------------------------------------------------

    /// <summary>Indica si el cambio de estado es posible desde el estado actual.</summary>
    public bool Allows(VacancyTransition transition) => transition switch {
        VacancyTransition.Pause => Status == VacancyStatus.Open,
        VacancyTransition.Resume => Status == VacancyStatus.Paused,
        VacancyTransition.Reopen => Status == VacancyStatus.Closed,
        _ => false,
    };

    /// <summary>Indica si se puede cerrar (está abierta o en pausa).</summary>
    public bool CanClose => Status != VacancyStatus.Closed;

    /// <summary>
    /// Indica si se puede reabrir con esa fecha prevista de cierre: una vacante abierta con la
    /// fecha ya pasada contaría la retención desde el pasado y sus CV se borrarían antes de tiempo.
    /// </summary>
    public bool CanReopenUntil(DateOnly closesOn, DateTimeOffset now) => closesOn >= Today(now).AddDays(-1);

    /// <summary>Aplica un cambio de estado. Al reabrir puede fijar una fecha prevista de cierre nueva.</summary>
    /// <exception cref="DomainException">Si no es posible desde el estado actual, o se reabre con la fecha prevista ya pasada.</exception>
    public void Apply(VacancyTransition transition, Guid actorId, DateTimeOffset now, DateOnly? closesOn = null) {

        if (!Allows(transition)) {
            throw new DomainException($"A {Status.ToString().ToLowerInvariant()} vacancy cannot {transition.ToString().ToLowerInvariant()}.");
        }

        if (closesOn is not null && transition != VacancyTransition.Reopen) {
            throw new DomainException("Only reopening can change the planned close date.");
        }

        if (transition == VacancyTransition.Reopen) {
            var planned = closesOn ?? ClosesOn;
            if (!CanReopenUntil(planned, now)) {
                throw new DomainException("The planned close date has passed: set a new one to reopen the vacancy.");
            }
            CheckClosesOn(planned, now);
            ClosesOn = planned;
        }

        switch (transition) {
            case VacancyTransition.Pause:
                Status = VacancyStatus.Paused;
                break;
            case VacancyTransition.Resume:
                Status = VacancyStatus.Open;
                break;
            case VacancyTransition.Reopen:
                Status = VacancyStatus.Open;
                CloseReason = null;
                ClosedOn = null;
                OpenedOn = Today(now);
                break;
        }

        SetUpdated(actorId, now);

    }

    /// <summary>Cierra la vacante: empieza a contar la retención.</summary>
    /// <exception cref="DomainException">Si ya está cerrada.</exception>
    public void Close(CloseReason reason, Guid actorId, DateTimeOffset now) {

        if (!CanClose) {
            throw new DomainException("The vacancy is already closed.");
        }

        Status = VacancyStatus.Closed;
        CloseReason = reason;
        ClosedOn = Today(now);
        SetUpdated(actorId, now);

    }

    // --- Derivados -----------------------------------------------------------------

    /// <summary>Día en que se borran sus CV: cierre (o cierre previsto) más la retención.</summary>
    public static DateOnly DeletesOn(DateOnly closesOn, DateOnly? closedOn, int retentionDays) =>
        (closedOn ?? closesOn).AddDays(retentionDays);

    /// <summary>Días que lleva (o llevó) abierta.</summary>
    public static int OpenDays(DateOnly openedOn, DateOnly? closedOn, DateOnly today) =>
        Math.Max(0, (closedOn ?? today).DayNumber - openedOn.DayNumber);

    // --- Internos ------------------------------------------------------------------

    /// <remarks>
    /// La fecha prevista solo se valida al crear o al cambiarla: editar el título de una vacante
    /// cuya fecha ya pasó (o de una cerrada) no debe exigir otra.
    /// </remarks>
    private void ApplyDetails(VacancyDetails details, DateTimeOffset now, bool isNew) {

        if (details.OwnerId == Guid.Empty) {
            throw new DomainException("Owner is required.");
        }

        if (isNew || details.ClosesOn != ClosesOn) {
            CheckClosesOn(details.ClosesOn, now);
        }

        if (!Enum.IsDefined(details.Modality)) {
            throw new DomainException("Modality is invalid.");
        }

        Title = DomainText.Required(details.Title, "Title", MaxTitleLength);
        Modality = details.Modality;
        OwnerId = details.OwnerId;
        ClosesOn = details.ClosesOn;

    }

    private List<Guid> ApplyRequirements(IReadOnlyList<RequirementDraft> drafts) {

        if (drafts.Count == 0) {
            throw new DomainException("A vacancy needs at least one requirement.");
        }

        if (drafts.Count > MaxRequirements) {
            throw new DomainException($"A vacancy cannot have more than {MaxRequirements} requirements.");
        }

        if (drafts.Any(draft => !Enum.IsDefined(draft.Kind))) {
            throw new DomainException("Requirement kind is invalid.");
        }

        var ids = drafts.Where(draft => draft.Id is not null).Select(draft => draft.Id!.Value).ToList();

        if (ids.Count != ids.Distinct().Count()) {
            throw new DomainException("A requirement cannot appear twice.");
        }

        // Un id ajeno no se convierte en un requisito nuevo: es un error del llamante.
        if (ids.Any(id => requirements.All(existing => existing.Id != id))) {
            throw new DomainException("The requirement does not belong to this vacancy.");
        }

        var texts = drafts.Select(draft => Requirement.NormalizeText(draft.Text)).ToList();
        if (texts.Count != texts.Distinct(StringComparer.OrdinalIgnoreCase).Count()) {
            throw new DomainException("Two requirements cannot have the same text.");
        }

        var changed = new List<Guid>();
        var kept = new List<Requirement>(drafts.Count);

        for (var position = 0; position < drafts.Count; position++) {

            var draft = drafts[position];
            var existing = draft.Id is { } id ? requirements.Single(requirement => requirement.Id == id) : null;

            if (existing is null) {
                var created = Requirement.Create(Guid.NewGuid(), this, draft.Text, draft.Kind, draft.Weight ?? Requirement.DefaultWeight(draft.Kind), position);
                changed.Add(created.Id);
                kept.Add(created);
                continue;
            }

            if (existing.Apply(draft.Text, draft.Kind, draft.Weight ?? existing.Weight, position)) {
                changed.Add(existing.Id);
            }

            kept.Add(existing);

        }

        requirements.Clear();
        requirements.AddRange(kept);

        return changed;

    }

    /// <remarks>UTC: un día de margen cubre a quien la fija por la tarde en Centroamérica (UTC-6).</remarks>
    private static void CheckClosesOn(DateOnly closesOn, DateTimeOffset now) {
        var today = Today(now);
        if (closesOn < today.AddDays(-1) || closesOn > today.AddDays(MaxDaysUntilClose)) {
            throw new DomainException($"ClosesOn must be between today and {MaxDaysUntilClose} days from now.");
        }
    }

    private static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

}