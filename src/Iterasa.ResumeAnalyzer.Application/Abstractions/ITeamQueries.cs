using Iterasa.ResumeAnalyzer.Application.Features.Vacancies;

namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Puerto de lectura del equipo de una empresa.</summary>
internal interface ITeamQueries {

    /// <summary>Miembros activos que pueden ser responsables de una vacante, por nombre.</summary>
    /// <remarks>
    /// Recibe la empresa en lugar de tomar la de la operación: lo llama una fábrica de la caché,
    /// que puede ejecutarse fuera del contexto de la petición.
    /// </remarks>
    Task<IReadOnlyList<OwnerOption>> ListOwnerOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

}