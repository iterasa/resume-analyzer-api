using Company.Product.Template.Application.Abstractions;
using Company.Product.Template.Infrastructure.Health;
using Company.Product.Template.Infrastructure.Persistence;
using Company.Product.Template.Infrastructure.Web;
using Company.Product.Template.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Company.Product.Template.Infrastructure;

/// <summary>
/// Registro de dependencias de la capa de infraestructura.
/// </summary>
public static class DependencyInjection {

    /// <summary>
    /// Registra los servicios de infraestructura: persistencia y health checks.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services) {

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(EnvironmentVariableReader.GetRequired(EnvironmentVariables.DatabaseConnectionString)));

        services.AddScoped<ICurrentPrincipal, CurrentPrincipal>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserQueries, UserQueries>();

        // Solo la sonda de readiness consulta la base de datos: la liveness no depende de
        // ella, para que un fallo de PostgreSQL no provoque el reinicio del contenedor.
        // Con tope de tiempo: la sonda es anónima y no debe quedarse colgada de una base de
        // datos que no responde.
        services
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name, tags: [HealthCheckTags.Ready], timeout: TimeSpan.FromSeconds(5));

        return services;

    }

}