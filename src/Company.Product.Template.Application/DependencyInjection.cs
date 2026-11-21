using Company.Product.Template.Application.Features.Users.Commands.CreateUser;
using Company.Product.Template.Application.Features.Users.Commands.SetUserStatus;
using Company.Product.Template.Application.Features.Users.Queries.GetUserById;
using Company.Product.Template.Application.Features.Users.Queries.GetUsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Company.Product.Template.Application;

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

        services.AddScoped<ICreateUserHandler, CreateUserHandler>();
        services.AddScoped<IGetUsersHandler, GetUsersHandler>();
        services.AddScoped<IGetUserByIdHandler, GetUserByIdHandler>();
        services.AddScoped<ISetUserStatusHandler, SetUserStatusHandler>();

        return services;

    }
}