using Company.Product.Template.Shared.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using IPNetwork = System.Net.IPNetwork;

namespace Company.Product.Template.Api.Http;

/// <summary>
/// Configuración de cabeceras reenviadas por proxys inversos.
/// </summary>
/// <remarks>
/// Sin esto, tras un balanceador o ingress la IP de origen y el esquema son los del proxy,
/// y así quedarían en el log. Está desactivado por defecto porque confiar en
/// <c>X-Forwarded-For</c> sin un proxy delante permitiría a cualquier cliente falsear su IP.
/// </remarks>
internal static class ProxyHeadersExtensions {

    /// <summary>
    /// Indica si el tratamiento de cabeceras reenviadas está activado.
    /// </summary>
    public static bool IsEnabled =>
        EnvironmentVariableReader.GetBool(EnvironmentVariables.ForwardedHeadersEnabled, false);

    /// <summary>
    /// Registra las opciones de cabeceras reenviadas a partir de variables de entorno.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddConfiguredForwardedHeaders(this IServiceCollection services) {

        services.Configure<ForwardedHeadersOptions>(options => {

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            var proxies = ParseProxies(EnvironmentVariableReader.GetOptional(EnvironmentVariables.ForwardedHeadersKnownProxies));
            var networks = ParseNetworks(EnvironmentVariableReader.GetOptional(EnvironmentVariables.ForwardedHeadersKnownNetworks));

            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in proxies) {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks) {
                options.KnownIPNetworks.Add(network);
            }

        });

        return services;

    }

    /// <summary>
    /// Indica si la configuración deja pasar cabeceras de cualquier origen por no
    /// declarar proxys ni redes de confianza.
    /// </summary>
    /// <remarks>
    /// Mira las listas ya troceadas, las mismas que se aplican: un valor como <c>,</c> no
    /// está vacío pero no declara ninguna entrada.
    /// </remarks>
    public static bool TrustsAnyProxy =>
        Split(EnvironmentVariableReader.GetOptional(EnvironmentVariables.ForwardedHeadersKnownProxies)).Length == 0
        && Split(EnvironmentVariableReader.GetOptional(EnvironmentVariables.ForwardedHeadersKnownNetworks)).Length == 0;

    private static IEnumerable<IPAddress> ParseProxies(string? raw) =>
        Split(raw).Select(value => IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException($"Invalid IP address in {EnvironmentVariables.ForwardedHeadersKnownProxies}: '{value}'."));

    private static IEnumerable<IPNetwork> ParseNetworks(string? raw) =>
        Split(raw).Select(value => IPNetwork.TryParse(value, out var network)
            ? network
            : throw new InvalidOperationException($"Invalid CIDR network in {EnvironmentVariables.ForwardedHeadersKnownNetworks}: '{value}'."));

    private static string[] Split(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

}