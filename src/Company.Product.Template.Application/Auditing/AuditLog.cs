using Microsoft.Extensions.Logging;

namespace Company.Product.Template.Application.Auditing;

/// <summary>
/// Registro estructurado de las operaciones de negocio y administración.
/// </summary>
/// <remarks>
/// Un único formato para todas las operaciones: permite filtrar el log por
/// <c>AuditAction</c>, <c>ActorId</c> o <c>AuditTarget</c> sin depender de cómo
/// redactó el mensaje cada handler.
/// </remarks>
internal static class AuditLog {

    /// <summary>
    /// Registra una operación completada con éxito.
    /// </summary>
    /// <param name="logger">Logger del handler.</param>
    /// <param name="actorId">Usuario que ejecuta la operación.</param>
    /// <param name="action">Acción ejecutada, en formato <c>recurso.verbo</c>.</param>
    /// <param name="target">Elemento afectado.</param>
    public static void Succeeded(ILogger logger, Guid actorId, string action, string target) =>
        logger.LogInformation(
            "Audit {AuditAction} on {AuditTarget} by {ActorId}: {AuditOutcome}",
            action,
            target,
            actorId,
            "succeeded");

    /// <summary>
    /// Registra una operación rechazada, con su motivo.
    /// </summary>
    /// <param name="logger">Logger del handler.</param>
    /// <param name="actorId">Usuario que ejecuta la operación.</param>
    /// <param name="action">Acción intentada, en formato <c>recurso.verbo</c>.</param>
    /// <param name="target">Elemento afectado.</param>
    /// <param name="reason">Motivo del rechazo.</param>
    public static void Rejected(ILogger logger, Guid actorId, string action, string target, string reason) =>
        logger.LogWarning(
            "Audit {AuditAction} on {AuditTarget} by {ActorId}: {AuditOutcome} ({AuditReason})",
            action,
            target,
            actorId,
            "rejected",
            reason);

}