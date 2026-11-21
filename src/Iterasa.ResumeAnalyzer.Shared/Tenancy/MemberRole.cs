namespace Iterasa.ResumeAnalyzer.Shared.Tenancy;

/// <summary>
/// Rol de una persona en una empresa. Es lo único que se asigna: los permisos salen de él
/// (<see cref="RolePermissions" />).
/// </summary>
public enum MemberRole {

    /// <summary>Administrador: todo, incluidos el equipo, la privacidad y los pagos.</summary>
    Admin,

    /// <summary>Reclutador: vacantes y candidatos.</summary>
    Recruiter,

    /// <summary>Gerente de contratación: la lista corta, sin revelar datos.</summary>
    HiringManager,

    /// <summary>Solo lectura: la lista corta.</summary>
    ReadOnly,

}