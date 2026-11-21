namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Para qué sirve una llave de un ámbito. Cada ámbito tiene dos llaves independientes, para
/// que comprometer o rotar una no afecte a la otra.
/// </summary>
internal enum KeyPurpose {

    /// <summary>Cifra los campos (<c>enc</c>).</summary>
    Encryption,

    /// <summary>Calcula los índices ciegos (<c>idx</c>).</summary>
    BlindIndex,

}

/// <summary>
/// Ciclo de vida de una llave.
/// </summary>
internal enum KeyStatus {

    /// <summary>Cifra y descifra. Una sola por ámbito y propósito.</summary>
    Active,

    /// <summary>Ya no cifra, pero descifra lo que quedó con ella mientras dura el recifrado.</summary>
    DecryptOnly,

    /// <summary>Ningún dato la usa: no se carga.</summary>
    Retired,

    /// <summary>Destruida (borrado criptográfico): lo cifrado con ella es ilegible.</summary>
    Destroyed,

}

/// <summary>
/// Representación en base de datos de <see cref="KeyPurpose" /> y <see cref="KeyStatus" />.
/// </summary>
internal static class KeyCodes {

    public static string ToCode(this KeyPurpose purpose) => purpose switch {
        KeyPurpose.Encryption => "enc",
        KeyPurpose.BlindIndex => "idx",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
    };

    public static KeyPurpose ToKeyPurpose(string code) => code switch {
        "enc" => KeyPurpose.Encryption,
        "idx" => KeyPurpose.BlindIndex,
        _ => throw new InvalidOperationException($"Unknown key purpose '{code}'."),
    };

    public static string ToCode(this KeyStatus status) => status switch {
        KeyStatus.Active => "active",
        KeyStatus.DecryptOnly => "decrypt_only",
        KeyStatus.Retired => "retired",
        KeyStatus.Destroyed => "destroyed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static KeyStatus ToKeyStatus(string code) => code switch {
        "active" => KeyStatus.Active,
        "decrypt_only" => KeyStatus.DecryptOnly,
        "retired" => KeyStatus.Retired,
        "destroyed" => KeyStatus.Destroyed,
        _ => throw new InvalidOperationException($"Unknown key status '{code}'."),
    };

}