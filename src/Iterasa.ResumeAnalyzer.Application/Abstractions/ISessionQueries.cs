namespace Iterasa.ResumeAnalyzer.Application.Abstractions;

/// <summary>Puerto de lectura de la sesión: el usuario y sus membresías activas, en cualquier empresa.</summary>
internal interface ISessionQueries {

    Task<SessionUserRow?> FindUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipRow>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);

}