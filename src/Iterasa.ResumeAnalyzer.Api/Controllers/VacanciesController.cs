using Asp.Versioning;
using Iterasa.ResumeAnalyzer.Api.Contracts;
using Iterasa.ResumeAnalyzer.Api.Extensions;
using Iterasa.ResumeAnalyzer.Api.Idempotency;
using Iterasa.ResumeAnalyzer.Api.RateLimiting;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.ChangeVacancyStatus;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CloseVacancy;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CreateVacancy;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateRequirements;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateVacancy;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetOwnerOptions;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancies;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancyById;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Vacancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Iterasa.ResumeAnalyzer.Api.Controllers;

/// <summary>
/// Vacantes de la empresa activa. Leer, cualquier miembro; crear, editar y cambiar de estado,
/// quien tenga <c>vacancies.manage</c>. Una vacante de otra empresa no existe (404).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vacancies")]
[EnableRateLimiting(RateLimitPolicies.PerClient)]
public sealed class VacanciesController(
    IGetVacanciesHandler getVacanciesHandler,
    IGetVacancyByIdHandler getVacancyByIdHandler,
    IGetOwnerOptionsHandler getOwnerOptionsHandler,
    ICreateVacancyHandler createVacancyHandler,
    IUpdateVacancyHandler updateVacancyHandler,
    IUpdateRequirementsHandler updateRequirementsHandler,
    ICloseVacancyHandler closeVacancyHandler,
    IChangeVacancyStatusHandler changeVacancyStatusHandler) : ControllerBase {

    /// <summary>Vacantes, las más recientes primero.</summary>
    /// <param name="status">Solo las de este estado.</param>
    /// <param name="page">Página, desde 1.</param>
    /// <param name="pageSize">Tamaño de página, de 1 a 200.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Página obtenida.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VacancySummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] VacancyStatus? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) {
        var result = await getVacanciesHandler.Handle(new GetVacanciesQuery(status, PageQuery.Create(page, pageSize)), cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Miembros que pueden ser responsables de una vacante.</summary>
    /// <response code="200">Lista, por nombre.</response>
    [HttpGet("owner-options")]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(IReadOnlyList<OwnerOption>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOwnerOptions(CancellationToken cancellationToken) {
        var result = await getOwnerOptionsHandler.Handle(cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Detalle de una vacante.</summary>
    /// <response code="200">Vacante encontrada.</response>
    /// <response code="404">No existe en la empresa activa.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) {
        var result = await getVacancyByIdHandler.Handle(new GetVacancyByIdQuery(id), cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Crea una vacante abierta.</summary>
    /// <remarks>Idempotente con <c>Idempotency-Key</c>.</remarks>
    /// <response code="201">Vacante creada.</response>
    /// <response code="400">Datos o requisitos no válidos, o responsable que no es del equipo.</response>
    [HttpPost]
    [Idempotent]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateVacancyRequest request, CancellationToken cancellationToken) {
        var result = await createVacancyHandler.Handle(
            new CreateVacancyCommand(request.Title, request.Modality!.Value, request.OwnerId!.Value, request.ClosesOn!.Value, ToInputs(request.Requirements!)),
            cancellationToken);
        return result.Match(created => CreatedAtAction(nameof(GetById), new { id = created.Id }, created), this.ToProblem);
    }

    /// <summary>Edita una vacante y sus requisitos.</summary>
    /// <remarks>Un requisito nuevo o con otro texto se vuelve a evaluar; los pesos se conservan.</remarks>
    /// <response code="200">Vacante editada.</response>
    /// <response code="400">Datos o requisitos no válidos.</response>
    /// <response code="404">No existe en la empresa activa.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVacancyRequest request, CancellationToken cancellationToken) {
        var result = await updateVacancyHandler.Handle(
            new UpdateVacancyCommand(id, request.Title, request.Modality!.Value, request.OwnerId!.Value, request.ClosesOn!.Value, ToInputs(request.Requirements!)),
            cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Cambia los requisitos y sus pesos.</summary>
    /// <remarks>Peso o tipo solo recalculan; un requisito nuevo o con otro texto se vuelve a evaluar.</remarks>
    /// <response code="200">Requisitos guardados.</response>
    /// <response code="400">Requisitos no válidos.</response>
    /// <response code="404">No existe en la empresa activa.</response>
    [HttpPut("{id:guid}/requirements")]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRequirements(Guid id, [FromBody] UpdateRequirementsRequest request, CancellationToken cancellationToken) {
        var result = await updateRequirementsHandler.Handle(
            new UpdateRequirementsCommand(id, [.. request.Requirements!.Select(item => new RequirementInput(item.Id, item.Text, item.Kind!.Value, item.Weight))]),
            cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Cierra la vacante: empieza la retención (auditado).</summary>
    /// <remarks>Idempotente con <c>Idempotency-Key</c>.</remarks>
    /// <response code="200">Vacante cerrada.</response>
    /// <response code="404">No existe en la empresa activa, o la postulación contratada no existe.</response>
    /// <response code="409">Ya estaba cerrada.</response>
    [HttpPost("{id:guid}/close")]
    [Idempotent]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseVacancyRequest request, CancellationToken cancellationToken) {
        var result = await closeVacancyHandler.Handle(new CloseVacancyCommand(id, request.Reason!.Value, request.HiredApplicationId, request.NotifyCandidates), cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    /// <summary>Reabre una vacante cerrada (auditado).</summary>
    /// <remarks>
    /// Sin cuerpo conserva la fecha prevista de cierre. Si ya pasó, hay que enviar otra: la
    /// retención de una vacante abierta cuenta desde esa fecha.
    /// </remarks>
    /// <response code="200">Vacante abierta.</response>
    /// <response code="400">Fecha prevista de cierre no válida.</response>
    /// <response code="404">No existe en la empresa activa.</response>
    /// <response code="409">No estaba cerrada, o su fecha prevista de cierre ya pasó.</response>
    [HttpPost("{id:guid}/reopen")]
    [Idempotent]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reopen(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ReopenVacancyRequest? request,
        CancellationToken cancellationToken) =>
        Change(id, VacancyTransition.Reopen, cancellationToken, request?.ClosesOn);

    /// <summary>Pausa una vacante abierta: no recibe CV ni avisa a nadie (auditado).</summary>
    /// <response code="200">Vacante en pausa.</response>
    /// <response code="404">No existe en la empresa activa.</response>
    /// <response code="409">No estaba abierta.</response>
    [HttpPost("{id:guid}/pause")]
    [Idempotent]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken) => Change(id, VacancyTransition.Pause, cancellationToken);

    /// <summary>Reanuda una vacante en pausa (auditado).</summary>
    /// <response code="200">Vacante abierta.</response>
    /// <response code="404">No existe en la empresa activa.</response>
    /// <response code="409">No estaba en pausa.</response>
    [HttpPost("{id:guid}/resume")]
    [Idempotent]
    [Authorize(Policy = Permissions.VacanciesManage)]
    [ProducesResponseType(typeof(VacancyDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) => Change(id, VacancyTransition.Resume, cancellationToken);

    private async Task<IActionResult> Change(Guid id, VacancyTransition transition, CancellationToken cancellationToken, DateOnly? closesOn = null) {
        var result = await changeVacancyStatusHandler.Handle(new ChangeVacancyStatusCommand(id, transition, closesOn), cancellationToken);
        return result.Match(Ok, this.ToProblem);
    }

    private static List<RequirementInput> ToInputs(IReadOnlyList<RequirementRequest> requirements) =>
        [.. requirements.Select(item => new RequirementInput(item.Id, item.Text, item.Kind!.Value, Weight: null))];

}