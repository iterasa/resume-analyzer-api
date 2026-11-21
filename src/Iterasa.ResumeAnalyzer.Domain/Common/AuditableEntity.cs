namespace Iterasa.ResumeAnalyzer.Domain.Common;

/// <summary>
/// Base de las entidades persistidas: auditoría y borrado lógico.
/// </summary>
/// <remarks>
/// La API no borra filas: <see cref="IsEnabled" /> es el mecanismo de baja, de modo que
/// el rastro de auditoría sobrevive a la eliminación.
/// Las transiciones de estado son <see langword="protected" /> a propósito: el alta y la
/// baja son estado del agregado, y exponerlas desde la clase base dejaba que cualquiera
/// las cambiase sin pasar por sus invariantes. Cada agregado publica su propio verbo, con
/// el nombre y las reglas que le correspondan.
/// El instante lo recibe cada verbo: el dominio no consulta el reloj, de modo que una
/// prueba fija la hora y la capa de aplicación decide de dónde sale.
/// </remarks>
internal abstract class AuditableEntity {

    /// <summary>Instante UTC de alta.</summary>
    public DateTimeOffset CreatedAtUtc { get; protected set; }

    /// <summary>Usuario que ejecutó el alta. <see cref="Guid.Empty" /> si fue el sistema.</summary>
    public Guid CreatedBy { get; protected set; }

    /// <summary>Instante UTC de la última modificación administrativa.</summary>
    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    /// <summary>Usuario que ejecutó la última modificación administrativa.</summary>
    public Guid UpdatedBy { get; protected set; }

    /// <summary>Estado de la baja lógica. La API nunca borra filas.</summary>
    public bool IsEnabled { get; protected set; }

    /// <summary>Fija las marcas de alta y deja la entidad habilitada. Solo desde el factory.</summary>
    protected void SetCreated(Guid actorId, DateTimeOffset now) {
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        CreatedBy = actorId;
        UpdatedBy = actorId;
        IsEnabled = true;
    }

    /// <summary>Actualiza las marcas de modificación. Cada verbo del agregado lo invoca al terminar.</summary>
    protected void SetUpdated(Guid actorId, DateTimeOffset now) {
        UpdatedAtUtc = now;
        UpdatedBy = actorId;
    }

    /// <summary>
    /// Baja lógica de la entidad.
    /// </summary>
    /// <param name="actorId">Usuario que ejecuta la baja.</param>
    /// <param name="now">Instante de la operación.</param>
    protected void MarkDisabled(Guid actorId, DateTimeOffset now) {
        IsEnabled = false;
        SetUpdated(actorId, now);
    }

    /// <summary>
    /// Reactiva una entidad dada de baja.
    /// </summary>
    /// <param name="actorId">Usuario que ejecuta la reactivación.</param>
    /// <param name="now">Instante de la operación.</param>
    protected void MarkEnabled(Guid actorId, DateTimeOffset now) {
        IsEnabled = true;
        SetUpdated(actorId, now);
    }

}