using System.Globalization;

namespace Iterasa.ResumeAnalyzer.Shared.Configuration;

/// <summary>
/// Lectura tipada de variables de entorno. Es la única puerta de entrada de
/// configuración de la aplicación: no se usa <c>appsettings.json</c>.
/// </summary>
public static class EnvironmentVariableReader {

    /// <summary>
    /// Lee una variable obligatoria.
    /// </summary>
    /// <param name="name">Nombre de la variable de entorno.</param>
    /// <returns>Valor de la variable.</returns>
    /// <exception cref="InvalidOperationException">Cuando la variable no está definida o está vacía.</exception>
    public static string GetRequired(string name) {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Missing environment variable: {name}")
            : value;
    }

    /// <summary>
    /// Lee una variable opcional sin valor por defecto.
    /// </summary>
    /// <param name="name">Nombre de la variable de entorno.</param>
    /// <returns>Valor de la variable o <see langword="null" /> si no está definida.</returns>
    public static string? GetOptional(string name) {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Lee una variable de texto con valor por defecto.
    /// </summary>
    /// <param name="name">Nombre de la variable de entorno.</param>
    /// <param name="defaultValue">Valor usado cuando la variable no está definida.</param>
    /// <returns>Valor de la variable o el valor por defecto.</returns>
    public static string GetString(string name, string defaultValue) =>
        GetOptional(name) ?? defaultValue;

    /// <summary>
    /// Lee una variable numérica con valor por defecto.
    /// </summary>
    /// <param name="name">Nombre de la variable de entorno.</param>
    /// <param name="defaultValue">Valor usado cuando la variable no está definida o no es un entero válido.</param>
    /// <returns>Valor numérico resuelto.</returns>
    public static int GetInt(string name, int defaultValue) {
        var raw = GetOptional(name);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    /// <summary>
    /// Lee una variable booleana con valor por defecto. Acepta <c>true/false</c>, <c>1/0</c>, <c>yes/no</c>, <c>on/off</c>.
    /// </summary>
    /// <remarks>
    /// Un valor no reconocible es un error, no el valor por defecto: estas variables
    /// encienden o apagan controles, y una errata (o unas comillas que <c>docker run
    /// --env-file</c> conserva) no debe dejar uno encendido sin que nadie se entere.
    /// </remarks>
    /// <param name="name">Nombre de la variable de entorno.</param>
    /// <param name="defaultValue">Valor usado cuando la variable no está definida.</param>
    /// <returns>Valor booleano resuelto.</returns>
    /// <exception cref="InvalidOperationException">Cuando la variable tiene un valor no reconocible.</exception>
    public static bool GetBool(string name, bool defaultValue) {
        var raw = GetOptional(name)?.Trim();

        return raw?.ToUpperInvariant() switch {
            null => defaultValue,
            "TRUE" or "1" or "YES" or "ON" => true,
            "FALSE" or "0" or "NO" or "OFF" => false,
            _ => throw new InvalidOperationException(
                $"Invalid boolean in environment variable {name}: '{raw}'. Use true/false, 1/0, yes/no or on/off.")
        };
    }

}