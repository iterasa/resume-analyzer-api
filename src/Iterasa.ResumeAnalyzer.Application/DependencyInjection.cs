using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.Register;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignIn;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SignOut;
using Iterasa.ResumeAnalyzer.Application.Features.Auth.Commands.SwitchTenant;
using Iterasa.ResumeAnalyzer.Application.Features.Me;
using Iterasa.ResumeAnalyzer.Application.Features.Me.Queries.GetSession;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.ChangeVacancyStatus;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CloseVacancy;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.CreateVacancy;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateRequirements;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Commands.UpdateVacancy;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetOwnerOptions;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancies;
using Iterasa.ResumeAnalyzer.Application.Features.Vacancies.Queries.GetVacancyById;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Iterasa.ResumeAnalyzer.Application;

/// <summary>
/// Registro de dependencias de la capa de aplicación.
/// </summary>
public static class DependencyInjection {
    /// <summary>
    /// Registra los servicios de la capa de aplicación.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services) {

        // El reloj es una dependencia como otra cualquiera: los casos de uso lo leen una
        // vez y pasan el instante al dominio, y las pruebas lo sustituyen por uno fijo.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SessionComposer>();
        services.AddScoped<VacancyReader>();

        services.AddScoped<IRegisterHandler, RegisterHandler>();
        services.AddScoped<ISignInHandler, SignInHandler>();
        services.AddScoped<ISignOutHandler, SignOutHandler>();
        services.AddScoped<ISwitchTenantHandler, SwitchTenantHandler>();
        services.AddScoped<IGetSessionHandler, GetSessionHandler>();

        services.AddScoped<ICreateVacancyHandler, CreateVacancyHandler>();
        services.AddScoped<IUpdateVacancyHandler, UpdateVacancyHandler>();
        services.AddScoped<IUpdateRequirementsHandler, UpdateRequirementsHandler>();
        services.AddScoped<ICloseVacancyHandler, CloseVacancyHandler>();
        services.AddScoped<IChangeVacancyStatusHandler, ChangeVacancyStatusHandler>();
        services.AddScoped<IGetVacanciesHandler, GetVacanciesHandler>();
        services.AddScoped<IGetVacancyByIdHandler, GetVacancyByIdHandler>();
        services.AddScoped<IGetOwnerOptionsHandler, GetOwnerOptionsHandler>();

        return services;

    }
}