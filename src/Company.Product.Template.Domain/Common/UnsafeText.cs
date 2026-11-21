using System.Buffers;
using System.Globalization;
using System.Text;

namespace Company.Product.Template.Domain.Common;

/// <summary>
/// Caracteres que no tienen sentido en un dato de texto corto (un nombre, un correo) y sí
/// efectos fuera de él: cortan o falsean líneas de log, se cuelan como invisibles o
/// invierten la lectura, y PostgreSQL rechaza el NUL.
/// </summary>
internal static class UnsafeText {

    /// <summary>
    /// Indica si el texto contiene un carácter de control, de formato o separador de línea
    /// o de párrafo, o un surrogate suelto.
    /// </summary>
    /// <remarks>
    /// Recorre puntos de código, no unidades UTF-16: los caracteres de formato fuera del
    /// plano básico (las etiquetas U+E0001…U+E007F) llegan como pares de surrogates, y
    /// cada mitad por separado no es de categoría Format.
    /// </remarks>
    /// <param name="value">Texto a comprobar.</param>
    /// <returns><c>true</c> si hay alguno.</returns>
    public static bool Contains(string value) {

        var remaining = value.AsSpan();

        while (!remaining.IsEmpty) {

            if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done) {
                return true;
            }

            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator) {
                return true;
            }

            remaining = remaining[consumed..];

        }

        return false;

    }

}