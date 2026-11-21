namespace Company.Product.Template.Infrastructure.Health;

/// <summary>
/// Etiquetas usadas para agrupar health checks por tipo de sonda.
/// </summary>
public static class HealthCheckTags {

    /// <summary>
    /// Checks de dependencias externas que determinan si la instancia puede atender tráfico.
    /// </summary>
    public const string Ready = "ready";

}