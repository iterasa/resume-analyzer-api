namespace Company.Product.Template.Shared;

/// <summary>
/// Página de un listado junto con la información necesaria para recorrer el resto.
/// </summary>
/// <typeparam name="T">Tipo de los elementos listados.</typeparam>
/// <param name="Items">Elementos de la página.</param>
/// <param name="Page">Número de página, empezando en uno.</param>
/// <param name="PageSize">Tamaño de página aplicado, que puede diferir del solicitado.</param>
/// <param name="TotalCount">Total de elementos que cumplen el criterio, no solo los de esta página.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount) {

    /// <summary>Número total de páginas disponibles.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Indica si queda alguna página por detrás de la actual.</summary>
    public bool HasNextPage => Page < TotalPages;

}