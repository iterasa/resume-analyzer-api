namespace Iterasa.ResumeAnalyzer.Shared.Configuration;

/// <summary>
/// Nombres de las variables de entorno usadas por la aplicación.
/// </summary>
public static class EnvironmentVariables {

    /// <summary>URL donde la aplicación escucha peticiones HTTP.</summary>
    public const string AspNetCoreUrls = "ASPNETCORE_URLS";

    /// <summary>Tamaño máximo en bytes del cuerpo de una petición. Por encima, Kestrel responde 413.</summary>
    public const string HttpMaxRequestBodyBytes = "HTTP_MAX_REQUEST_BODY_BYTES";

    /// <summary>Cadena de conexión principal a la base de datos.</summary>
    public const string DatabaseConnectionString = "DATABASE_CONNECTION_STRING";

    /// <summary>Nivel mínimo de log: <c>Verbose</c>, <c>Debug</c>, <c>Information</c>, <c>Warning</c>, <c>Error</c> o <c>Fatal</c>.</summary>
    public const string LogLevel = "LOG_LEVEL";

    /// <summary>Formato de salida del log: <c>json</c> (estructurado, por defecto) o <c>text</c> (legible en desarrollo).</summary>
    public const string LogFormat = "LOG_FORMAT";

    /// <summary>Título mostrado en la documentación OpenAPI. Por defecto, el nombre de la aplicación.</summary>
    public const string ApiTitle = "API_TITLE";

    /// <summary>Descripción mostrada en la documentación OpenAPI.</summary>
    public const string ApiDescription = "API_DESCRIPTION";

    /// <summary>Activa o desactiva la exposición de la documentación OpenAPI.</summary>
    public const string SwaggerEnabled = "SWAGGER_ENABLED";

    /// <summary>Activa CORS. Solo tiene sentido con un cliente de navegador en otro origen.</summary>
    public const string CorsEnabled = "CORS_ENABLED";

    /// <summary>
    /// Orígenes de navegador admitidos, separados por comas: <c>https://app.example.com</c>,
    /// <c>http://localhost:5173</c> o <c>https://*.example.com</c> para todos los subdominios.
    /// Nunca <c>*</c>.
    /// </summary>
    public const string CorsAllowedOrigins = "CORS_ALLOWED_ORIGINS";

    /// <summary>
    /// Permite peticiones con credenciales de navegador (cookies, autenticación HTTP). El
    /// token Bearer en <c>Authorization</c> no lo necesita.
    /// </summary>
    public const string CorsAllowCredentials = "CORS_ALLOW_CREDENTIALS";

    /// <summary>Segundos que el navegador puede cachear la respuesta a un preflight.</summary>
    public const string CorsPreflightMaxAgeSeconds = "CORS_PREFLIGHT_MAX_AGE_SECONDS";

    /// <summary>Activa el tratamiento de las cabeceras reenviadas por un proxy inverso.</summary>
    public const string ForwardedHeadersEnabled = "FORWARDED_HEADERS_ENABLED";

    /// <summary>Direcciones IP de los proxys de confianza, separadas por comas.</summary>
    public const string ForwardedHeadersKnownProxies = "FORWARDED_HEADERS_KNOWN_PROXIES";

    /// <summary>Redes CIDR de confianza (por ejemplo <c>10.0.0.0/8</c>), separadas por comas.</summary>
    public const string ForwardedHeadersKnownNetworks = "FORWARDED_HEADERS_KNOWN_NETWORKS";

}