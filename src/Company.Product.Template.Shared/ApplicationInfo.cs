namespace Company.Product.Template.Shared;

/// <summary>
/// Identidad de la aplicación, en un único sitio.
/// </summary>
/// <remarks>
/// La usan el logging, la telemetría y la documentación OpenAPI. Estaba repetida como
/// literal en cada uno de los tres, que es como el nombre de un proyecto acaba
/// desincronizado consigo mismo. El script de renombrado la actualiza aquí y basta.
/// </remarks>
public static class ApplicationInfo {

    /// <summary>Nombre de la aplicación.</summary>
    public const string Name = "Company.Product.Template";

}