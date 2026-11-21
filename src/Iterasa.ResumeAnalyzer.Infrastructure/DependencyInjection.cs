using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Application.Common;
using Iterasa.ResumeAnalyzer.Infrastructure.Caching;
using Iterasa.ResumeAnalyzer.Infrastructure.Health;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.DataProtection;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.Passwords;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;
using Iterasa.ResumeAnalyzer.Infrastructure.Tenancy;
using Iterasa.ResumeAnalyzer.Infrastructure.Web;
using Iterasa.ResumeAnalyzer.Shared;
using Iterasa.ResumeAnalyzer.Shared.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Iterasa.ResumeAnalyzer.Infrastructure;

/// <summary>
/// Registro de dependencias de la capa de infraestructura.
/// </summary>
public static class DependencyInjection {

    /// <summary>
    /// Registra los servicios de infraestructura: persistencia, cifrado de campos, sesiones,
    /// contraseñas, caché, idempotencia, Data Protection y health checks.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) {

        services.AddInfrastructureOptions();

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(EnvironmentVariableReader.GetRequired(EnvironmentVariables.DatabaseConnectionString)));

        services.AddFieldEncryption();

        services.AddScoped<ICurrentPrincipal, CurrentPrincipal>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ITenantScope, TenantScope>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IVacancyRepository, VacancyRepository>();
        services.AddScoped<ISessionQueries, SessionQueries>();
        services.AddScoped<ITenantQueries, TenantQueries>();
        services.AddScoped<ITeamQueries, TeamQueries>();
        services.AddScoped<IVacancyQueries, VacancyQueries>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddSingleton<IPublicLinks, PublicLinks>();

        services.AddScoped<SessionStore>();
        services.AddScoped<ISessionStore>(provider => provider.GetRequiredService<SessionStore>());
        services.AddScoped<ISessionAuthenticator>(provider => provider.GetRequiredService<SessionStore>());

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddHybridCache();
        services.AddSingleton<ICacheService, HybridCacheService>();

        // Las llaves del antiforgery, compartidas por todas las instancias y cifradas (ADR 0017).
        services.AddDataProtection().SetApplicationName(ApplicationInfo.Name);
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider => new ConfigureOptions<KeyManagementOptions>(options =>
            options.XmlRepository = new DatabaseXmlRepository(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<TimeProvider>())));

        services.AddHostedService<ExpiredRecordPurgeService>();

        // Solo la sonda de readiness consulta la base de datos: la liveness no depende de
        // ella, para que un fallo de PostgreSQL no provoque el reinicio del contenedor.
        // Con tope de tiempo: la sonda es anónima y no debe quedarse colgada de una base de
        // datos que no responde.
        services
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name, tags: [HealthCheckTags.Ready], timeout: TimeSpan.FromSeconds(5));

        return services;

    }

    /// <remarks>
    /// Las variables se leen dentro de los delegados de configuración y se validan al arrancar:
    /// la aplicación no llega a escuchar peticiones con configuración inválida.
    /// </remarks>
    private static IServiceCollection AddInfrastructureOptions(this IServiceCollection services) {

        services
            .AddOptions<SessionOptions>()
            .Configure(options => {
                options.IdleTimeout = TimeSpan.FromMinutes(EnvironmentVariableReader.GetInt(EnvironmentVariables.SessionIdleMinutes, SessionOptions.DefaultIdleMinutes));
                options.AbsoluteLifetime = TimeSpan.FromHours(EnvironmentVariableReader.GetInt(EnvironmentVariables.SessionAbsoluteHours, SessionOptions.DefaultAbsoluteHours));
            })
            .Validate(options => options.IdleTimeout > TimeSpan.Zero, $"{EnvironmentVariables.SessionIdleMinutes} must be greater than zero.")
            .Validate(options => options.AbsoluteLifetime >= options.IdleTimeout, $"{EnvironmentVariables.SessionAbsoluteHours} must cover at least {EnvironmentVariables.SessionIdleMinutes}.")
            .ValidateOnStart();

        services
            .AddOptions<PasswordHashingOptions>()
            .Configure(options => {
                options.Algorithm = ParseAlgorithm() ?? PasswordHashAlgorithm.Argon2id;
                options.Argon2MemoryKib = EnvironmentVariableReader.GetInt(EnvironmentVariables.Argon2MemoryKib, options.Argon2MemoryKib);
                options.Argon2Iterations = EnvironmentVariableReader.GetInt(EnvironmentVariables.Argon2Iterations, options.Argon2Iterations);
                options.Argon2Parallelism = EnvironmentVariableReader.GetInt(EnvironmentVariables.Argon2Parallelism, options.Argon2Parallelism);
            })
            .Validate(_ => ParseAlgorithm() is not null, $"{EnvironmentVariables.PasswordHashAlgorithm} must be 'argon2id' or 'pbkdf2-sha256'.")
            .Validate(options => options.Argon2MemoryKib >= PasswordHashingOptions.MinimumArgon2MemoryKib, $"{EnvironmentVariables.Argon2MemoryKib} must be at least {PasswordHashingOptions.MinimumArgon2MemoryKib} KiB.")
            .Validate(options => options.Argon2Iterations >= 1 && options.Argon2Parallelism >= 1, $"{EnvironmentVariables.Argon2Iterations} and {EnvironmentVariables.Argon2Parallelism} must be at least 1.")
            .ValidateOnStart();

        services.Configure<SignInOptions>(options => {
            options.MaxFailedAttempts = Math.Max(1, EnvironmentVariableReader.GetInt(EnvironmentVariables.AuthMaxFailedSignIns, 5));
            options.LockoutDuration = TimeSpan.FromMinutes(Math.Max(1, EnvironmentVariableReader.GetInt(EnvironmentVariables.AuthLockoutMinutes, 15)));
        });

        services
            .AddOptions<PublicLinksOptions>()
            .Configure(options => options.AppPublicUrl = EnvironmentVariableReader.GetOptional(EnvironmentVariables.AppPublicUrl)?.Trim().TrimEnd('/'))
            .Validate(
                options => options.AppPublicUrl is null
                    || (Uri.TryCreate(options.AppPublicUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" && uri.AbsolutePath == "/"),
                $"{EnvironmentVariables.AppPublicUrl} must be an origin such as https://app.criba.gt.")
            .ValidateOnStart();

        return services;

    }

    private static PasswordHashAlgorithm? ParseAlgorithm() =>
        EnvironmentVariableReader.GetString(EnvironmentVariables.PasswordHashAlgorithm, "argon2id").Trim().ToLowerInvariant() switch {
            "argon2id" => PasswordHashAlgorithm.Argon2id,
            "pbkdf2-sha256" => PasswordHashAlgorithm.Pbkdf2Sha256,
            _ => null,
        };

}