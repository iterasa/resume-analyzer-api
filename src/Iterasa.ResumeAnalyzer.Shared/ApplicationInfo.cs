namespace Iterasa.ResumeAnalyzer.Shared;

/// <summary>
/// Identidad de la aplicación, en un único sitio.
/// </summary>
/// <remarks>
/// La usan el logging, la telemetría y la documentación OpenAPI. Estaba repetida como
/// literal en cada uno de los tres, que es como el nombre de un proyecto acaba
/// desincronizado consigo mismo. Para cambiarlo basta con hacerlo aquí.
/// </remarks>
public static class ApplicationInfo {

    /// <summary>Nombre de la aplicación.</summary>
    public const string Name = "Iterasa.ResumeAnalyzer";

}