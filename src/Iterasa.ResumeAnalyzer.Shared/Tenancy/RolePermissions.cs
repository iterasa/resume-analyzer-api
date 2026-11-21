namespace Iterasa.ResumeAnalyzer.Shared.Tenancy;

/// <summary>
/// Qué permite cada rol. Está en código, no en la base: el catálogo de roles es fijo y cada
/// endpoint lo exige al compilar.
/// </summary>
public static class RolePermissions {

    private static readonly Dictionary<MemberRole, IReadOnlySet<string>> ByRole = new() {
        [MemberRole.Admin] = new HashSet<string>(Permissions.All, StringComparer.Ordinal),
        [MemberRole.Recruiter] = new HashSet<string>(StringComparer.Ordinal) {
            Permissions.VacanciesManage,
            Permissions.ApplicationsViewAll,
            Permissions.ApplicationsViewShortlist,
            Permissions.ApplicationsMove,
            Permissions.CandidatesReveal,
            Permissions.NotesWrite,
            Permissions.ReportsView,
        },
        [MemberRole.HiringManager] = new HashSet<string>(StringComparer.Ordinal) {
            Permissions.ApplicationsViewShortlist,
            Permissions.ApplicationsMove,
            Permissions.NotesWrite,
        },
        [MemberRole.ReadOnly] = new HashSet<string>(StringComparer.Ordinal) {
            Permissions.ApplicationsViewShortlist,
        },
    };

    /// <summary>Permisos de un rol, en el orden del catálogo.</summary>
    /// <param name="role">Rol.</param>
    /// <returns>Permisos concedidos.</returns>
    public static IReadOnlyList<string> Of(MemberRole role) =>
        [.. Permissions.All.Where(ByRole[role].Contains)];

    /// <summary>Indica si el rol concede el permiso.</summary>
    /// <param name="role">Rol.</param>
    /// <param name="permission">Permiso del catálogo.</param>
    /// <returns><see langword="true" /> si lo concede.</returns>
    public static bool Grants(MemberRole role, string permission) => ByRole[role].Contains(permission);

}