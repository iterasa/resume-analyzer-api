using Company.Product.Template.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Company.Product.Template.Infrastructure.Health;

/// <summary>
/// Comprueba la conectividad con la base de datos para la sonda de readiness.
/// </summary>
/// <remarks>
/// Comprueba conectividad real contra PostgreSQL. Se etiqueta como <c>ready</c> para
/// que solo afecte a la sonda de readiness y nunca tumbe la de liveness.
/// </remarks>
internal sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck {

    /// <summary>
    /// Nombre con el que se publica el check en el reporte de salud.
    /// </summary>
    public const string Name = "database";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {

        try {

            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database connection is available.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");

        } catch (Exception exception) when (exception is not OperationCanceledException) {
            return HealthCheckResult.Unhealthy("Database connectivity check failed.", exception);
        }

    }

}