namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>
/// Hash de contraseñas. El algoritmo es de la infraestructura (ADR 0013); el dominio solo
/// guarda el hash.
/// </summary>
internal interface IPasswordHasher {

    /// <summary>Hash serializado, con su algoritmo y parámetros.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifica una contraseña. Con <paramref name="passwordHash" /> nulo verifica contra un hash
    /// de relleno y devuelve <see langword="false" />: tarda lo mismo exista o no la cuenta.
    /// </summary>
    bool Verify(string password, string? passwordHash);

    /// <summary>Indica si el hash se generó con otros parámetros y conviene rehacerlo.</summary>
    bool NeedsRehash(string passwordHash);

}