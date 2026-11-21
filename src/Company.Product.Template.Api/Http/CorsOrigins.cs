using System.Diagnostics.CodeAnalysis;

namespace Company.Product.Template.Api.Http;

/// <summary>
/// Validación y normalización de los orígenes que admite la política CORS.
/// </summary>
/// <remarks>
/// Un origen es <c>esquema://host[:puerto]</c>, sin ruta ni credenciales, y se compara
/// literalmente: <c>https://app.example.com</c> no admite <c>http://app.example.com</c>,
/// ni <c>https://app.example.com:8443</c>, ni <c>https://app.example.com.evil.com</c>.
/// <c>https://*.example.com</c> admite cualquier subdominio directo o anidado, pero no
/// el dominio raíz. <c>*</c> a secas no se admite nunca: un origen cualquiera con
/// credenciales es un fallo de seguridad, y sin credenciales solo tiene sentido en una
/// API pública, que no es el caso de esta plantilla.
/// </remarks>
internal static class CorsOrigins {

    private const string WildcardMarker = "://*.";

    /// <summary>
    /// Normaliza un origen: esquema y host en minúsculas, sin barra final ni puerto por defecto.
    /// </summary>
    /// <param name="raw">Origen tal y como llegó de la configuración.</param>
    /// <param name="normalized">Origen normalizado, o <see langword="null" /> si no es válido.</param>
    /// <returns><see langword="true" /> si el origen es válido.</returns>
    public static bool TryNormalize(string raw, [NotNullWhen(true)] out string? normalized) {

        normalized = null;

        var candidate = raw.Trim();

        if (candidate.Length == 0 || candidate == "*" || string.Equals(candidate, "null", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        // Comodín de subdominios: se valida el resto como un origen normal y se vuelve a
        // anteponer, porque Uri no admite '*' como etiqueta de host.
        var wildcard = candidate.IndexOf(WildcardMarker, StringComparison.Ordinal);

        if (wildcard > 0) {

            var scheme = candidate[..wildcard];
            var rest = candidate[(wildcard + WildcardMarker.Length)..];

            if (!TryNormalizeExact($"{scheme}://{rest}", out var parent) || !parent[(parent.IndexOf("://", StringComparison.Ordinal) + 3)..].Contains('.')) {
                return false;
            }

            normalized = parent.Insert(parent.IndexOf("://", StringComparison.Ordinal) + 3, "*.");
            return true;

        }

        return TryNormalizeExact(candidate, out normalized);

    }

    /// <summary>Indica si el origen, ya normalizado, usa el comodín de subdominios.</summary>
    /// <param name="origin">Origen normalizado.</param>
    /// <returns><see langword="true" /> si admite subdominios.</returns>
    public static bool IsWildcardSubdomain(string origin) => origin.Contains(WildcardMarker, StringComparison.Ordinal);

    private static bool TryNormalizeExact(string candidate, [NotNullWhen(true)] out string? normalized) {

        normalized = null;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.HostNameType == UriHostNameType.Unknown
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0) {
            return false;
        }

        // GetLeftPart(Authority) ya pone esquema y host en minúsculas y omite el puerto por defecto.
        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;

    }

}