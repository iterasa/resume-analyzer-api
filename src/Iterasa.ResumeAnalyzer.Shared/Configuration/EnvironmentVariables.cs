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

    /// <summary>
    /// Llave maestra del cifrado de campos: <c>aws-kms</c> o <c>local</c> (solo desarrollo y
    /// pruebas). Obligatoria.
    /// </summary>
    public const string FieldEncryptionKekProvider = "FIELD_ENCRYPTION_KEK_PROVIDER";

    /// <summary>Id, ARN o alias de la llave de AWS KMS que envuelve las llaves de datos.</summary>
    public const string FieldEncryptionKmsKeyId = "FIELD_ENCRYPTION_KMS_KEY_ID";

    /// <summary>Llave maestra local: 32 bytes en base64. Nunca en producción.</summary>
    public const string FieldEncryptionLocalKek = "FIELD_ENCRYPTION_LOCAL_KEK";

    /// <summary>Minutos de inactividad tras los que una sesión vence.</summary>
    public const string SessionIdleMinutes = "SESSION_IDLE_MINUTES";

    /// <summary>Horas desde el inicio de sesión tras las que una sesión vence aunque se use.</summary>
    public const string SessionAbsoluteHours = "SESSION_ABSOLUTE_HOURS";

    /// <summary>
    /// Algoritmo de los hashes de contraseña nuevos: <c>argon2id</c> (por defecto) o
    /// <c>pbkdf2-sha256</c> (FIPS-140). Los existentes se verifican con el suyo y se rehacen
    /// en el siguiente inicio de sesión.
    /// </summary>
    public const string PasswordHashAlgorithm = "PASSWORD_HASH_ALGORITHM";

    /// <summary>Memoria de Argon2id en KiB. OWASP: 19456 con 2 iteraciones.</summary>
    public const string Argon2MemoryKib = "ARGON2_MEMORY_KIB";

    /// <summary>Iteraciones de Argon2id.</summary>
    public const string Argon2Iterations = "ARGON2_ITERATIONS";

    /// <summary>Paralelismo de Argon2id.</summary>
    public const string Argon2Parallelism = "ARGON2_PARALLELISM";

    /// <summary>Intentos fallidos consecutivos que bloquean el inicio de sesión.</summary>
    public const string AuthMaxFailedSignIns = "AUTH_MAX_FAILED_SIGN_INS";

    /// <summary>Minutos que dura el bloqueo por intentos fallidos.</summary>
    public const string AuthLockoutMinutes = "AUTH_LOCKOUT_MINUTES";

    /// <summary>Activa o desactiva el limitador de peticiones.</summary>
    public const string RateLimitEnabled = "RATE_LIMIT_ENABLED";

    /// <summary>Peticiones por ventana y usuario en los endpoints de negocio.</summary>
    public const string RateLimitPermitLimit = "RATE_LIMIT_PERMIT_LIMIT";

    /// <summary>Duración en segundos de la ventana de los endpoints de negocio.</summary>
    public const string RateLimitWindowSeconds = "RATE_LIMIT_WINDOW_SECONDS";

    /// <summary>Peticiones por ventana e IP en inicio de sesión y registro.</summary>
    public const string RateLimitAuthPermitLimit = "RATE_LIMIT_AUTH_PERMIT_LIMIT";

    /// <summary>Duración en segundos de la ventana de inicio de sesión y registro.</summary>
    public const string RateLimitAuthWindowSeconds = "RATE_LIMIT_AUTH_WINDOW_SECONDS";

    /// <summary>Horas durante las que una clave de idempotencia reproduce su respuesta.</summary>
    public const string IdempotencyKeyTtlHours = "IDEMPOTENCY_KEY_TTL_HOURS";

    /// <summary>Minutos entre limpiezas de sesiones vencidas y claves de idempotencia caducadas. Cero la desactiva.</summary>
    public const string PurgeIntervalMinutes = "PURGE_INTERVAL_MINUTES";

    /// <summary>
    /// Origen público de la web (<c>https://app.criba.gt</c>), para los enlaces que la API
    /// entrega, como el del formulario de postulación. Sin él, esos enlaces van vacíos.
    /// </summary>
    public const string AppPublicUrl = "APP_PUBLIC_URL";

}