using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Purga periódica de lo que caduca: sesiones vencidas o revocadas y claves de idempotencia.
/// </summary>
/// <remarks>
/// Una sesión vencida ya no autentica; su fila solo ocupa. Nunca interrumpe el proceso: un fallo
/// se registra y se reintenta en el siguiente ciclo.
/// </remarks>
internal sealed class ExpiredRecordPurgeService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ExpiredRecordPurgeService> logger) : BackgroundService {

    private const int DefaultIntervalMinutes = 60;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {

        var intervalMinutes = EnvironmentVariableReader.GetInt(EnvironmentVariables.PurgeIntervalMinutes, DefaultIntervalMinutes);

        if (intervalMinutes <= 0) {
            logger.LogInformation("Expired record purge is disabled by configuration");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes), clock);

        try {
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                await PurgeAsync(stoppingToken);
            }
        } catch (OperationCanceledException) {
            // Parada ordenada del host.
        }

    }

    private async Task PurgeAsync(CancellationToken cancellationToken) {

        try {

            using var scope = scopeFactory.CreateScope();
            var now = clock.GetUtcNow();

            var sessions = await scope.ServiceProvider.GetRequiredService<SessionStore>().PurgeAsync(now, cancellationToken);
            var keys = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().PurgeExpiredAsync(now, cancellationToken);

            if (sessions > 0 || keys > 0) {
                logger.LogInformation("Purged {SessionCount} expired sessions and {IdempotencyKeyCount} expired idempotency keys", sessions, keys);
            }

        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.LogError(exception, "Expired record purge failed; it will be retried on the next cycle");
        }

    }

}