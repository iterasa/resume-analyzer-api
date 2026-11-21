namespace Iterasa.ResumeAnalyzer.Shared;

/// <summary>
/// Tramo solicitado de un listado.
/// </summary>
/// <remarks>
/// Se construye siempre con <see cref="Create" />, que ajusta los valores a un rango
/// válido en lugar de rechazarlos: un tamaño de página desmedido es la vía más fácil de
/// convertir un listado en una denegación de servicio, y no merece un error del llamante.
/// </remarks>
public sealed record PageQuery {

    /// <summary>Tamaño de página aplicado cuando el llamante no indica ninguno.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Tamaño de página máximo admitido.</summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// Número de página máximo admitido: el mayor con el que <see cref="Skip" /> cabe en
    /// un <see cref="int" /> para cualquier tamaño de página válido.
    /// </summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    private PageQuery(int page, int pageSize) {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Número de página solicitado, empezando en uno.</summary>
    public int Page { get; }

    /// <summary>Número de elementos por página.</summary>
    public int PageSize { get; }

    /// <summary>Elementos que la consulta debe saltar para situarse en la página.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Crea un tramo válido a partir de los valores recibidos, ajustándolos al rango admitido.
    /// </summary>
    /// <param name="page">Página solicitada. Se ajusta entre uno y <see cref="MaxPage" />.</param>
    /// <param name="pageSize">Tamaño solicitado. Se ajusta entre uno y <see cref="MaxPageSize" />.</param>
    /// <returns>Tramo normalizado.</returns>
    public static PageQuery Create(int? page = null, int? pageSize = null) =>
        new(Math.Clamp(page ?? 1, 1, MaxPage), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

}