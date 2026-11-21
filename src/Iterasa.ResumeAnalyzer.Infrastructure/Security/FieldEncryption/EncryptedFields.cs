namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Inventario de los campos cifrados: el único sitio donde se declaran. La configuración de
/// EF de cada entidad los marca con <c>IsEncrypted(...)</c> y una prueba de arquitectura
/// comprueba que modelo e inventario coinciden.
/// </summary>
/// <remarks>
/// Qué campos se cifran lo decide el equipo (ADR 0015). Para añadir uno: declararlo aquí,
/// marcarlo en su configuración y generar la migración.
/// </remarks>
internal static class EncryptedFields {

    /// <summary>Correo de un usuario. Llaves de plataforma: un usuario puede estar en varias empresas.</summary>
    public static readonly EncryptedField UserEmail = new(
        "user", "email", CryptoScopeKind.Platform, new BlindIndexSpec(Normalizers.Email, Unique: true));

    /// <summary>Todos los campos cifrados.</summary>
    public static IReadOnlyList<EncryptedField> All { get; } = [UserEmail];

    private static readonly Dictionary<string, EncryptedField> byName = All.ToDictionary(field => field.Name, StringComparer.Ordinal);

    /// <summary>Busca un campo por <c>tabla.columna</c>.</summary>
    /// <exception cref="InvalidOperationException">Si no está en el inventario.</exception>
    public static EncryptedField Get(string name) =>
        byName.TryGetValue(name, out var field)
            ? field
            : throw new InvalidOperationException($"Field {name} is marked as encrypted but is not declared in EncryptedFields.");

}