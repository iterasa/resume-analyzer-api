using System.Globalization;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Un campo cifrado: dónde vive, con qué llaves se cifra y, si se busca, cómo.
/// </summary>
/// <param name="Table">Tabla de la columna.</param>
/// <param name="Column">Columna cifrada.</param>
/// <param name="Scope">Llaves de plataforma o de la empresa de la operación.</param>
/// <param name="Search">Índice ciego para buscarlo, o <see langword="null" /> si no se busca.</param>
internal sealed record EncryptedField(string Table, string Column, CryptoScopeKind Scope, BlindIndexSpec? Search = null) {

    /// <summary>Nombre único: <c>tabla.columna</c>. Es lo que se registra en el modelo.</summary>
    public string Name => $"{Table}.{Column}";

    /// <summary>Propiedad sombra con el índice ciego (<c>&lt;Columna&gt;Bidx</c>).</summary>
    public string BlindIndexProperty => $"{Pascal(Column)}Bidx";

    /// <summary>Propiedad sombra con la versión de la llave del índice.</summary>
    public string BlindIndexVersionProperty => $"{Pascal(Column)}BidxVersion";

    /// <summary>Columna del índice ciego.</summary>
    public string BlindIndexColumn => $"{Column}_bidx";

    /// <summary>Columna con la versión de la llave del índice.</summary>
    public string BlindIndexVersionColumn => $"{Column}_bidx_v";

    /// <summary>
    /// Etiqueta que separa los índices de campos distintos: el mismo valor da índices
    /// distintos en dos campos. Incluye la normalización, porque cambiarla obliga a reindexar.
    /// </summary>
    public string IndexLabel => Search is null
        ? throw new InvalidOperationException($"Field {Name} is not searchable.")
        : $"{Name}|{Search.Normalizer.Name}.v{Search.Normalizer.Version.ToString(CultureInfo.InvariantCulture)}";

    private static string Pascal(string snake) => string.Concat(
        snake.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

}

/// <summary>
/// Búsqueda exacta por índice ciego: HMAC del valor normalizado.
/// </summary>
/// <param name="Normalizer">Normalización compartida por la escritura y la consulta.</param>
/// <param name="Unique">Si el valor es único en su ámbito (restricción sobre el índice).</param>
/// <remarks>
/// La búsqueda por palabras y por prefijo (tabla de tokens) llega con el primer campo que
/// la necesite: el nombre del candidato. Ver ADR 0015.
/// </remarks>
internal sealed record BlindIndexSpec(Normalizer Normalizer, bool Unique);