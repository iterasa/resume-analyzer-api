namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.Passwords;

/// <summary>Algoritmos con los que la aplicación puede generar hashes de contraseñas.</summary>
internal enum PasswordHashAlgorithm {

    /// <summary>Argon2id (RFC 9106): la recomendación principal de OWASP.</summary>
    Argon2id,

    /// <summary>PBKDF2-HMAC-SHA256: la opción aprobada por FIPS-140.</summary>
    Pbkdf2Sha256

}

/// <summary>
/// Parámetros de los hashes de contraseñas.
/// </summary>
/// <remarks>
/// Los valores por defecto de Argon2id son la configuración de referencia de OWASP
/// (m=19 MiB, t=2, p=1). Subirlos es la forma de seguir el ritmo del hardware: el hash
/// serializa sus parámetros, así que los almacenados siguen verificando con los suyos y se
/// rehacen con los nuevos en el siguiente inicio de sesión.
/// </remarks>
internal sealed class PasswordHashingOptions {

    /// <summary>Memoria mínima admitida: la más baja de las configuraciones que OWASP considera equivalentes.</summary>
    public const int MinimumArgon2MemoryKib = 7168;

    /// <summary>Algoritmo de los hashes nuevos.</summary>
    public PasswordHashAlgorithm Algorithm { get; set; } = PasswordHashAlgorithm.Argon2id;

    /// <summary>Memoria de Argon2id en KiB.</summary>
    public int Argon2MemoryKib { get; set; } = 19456;

    /// <summary>Iteraciones de Argon2id.</summary>
    public int Argon2Iterations { get; set; } = 2;

    /// <summary>Paralelismo de Argon2id.</summary>
    public int Argon2Parallelism { get; set; } = 1;

}