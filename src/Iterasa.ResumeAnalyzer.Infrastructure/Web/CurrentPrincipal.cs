using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Web;

/// <summary>
/// Lee la identidad de la petición en curso de los claims de su sesión. Sin sesión, toda
/// petición es anónima.
/// </summary>
internal sealed class CurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal {

    /// <inheritdoc />
    public Guid UserId => ReadGuid(SessionClaims.UserId);

    /// <inheritdoc />
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    /// <inheritdoc />
    public Guid SessionId => ReadGuid(SessionClaims.SessionId);

    /// <inheritdoc />
    public Guid TenantId => ReadGuid(SessionClaims.TenantId);

    /// <inheritdoc />
    public MemberRole? Role =>
        IsAuthenticated && Enum.TryParse<MemberRole>(httpContextAccessor.HttpContext?.User.FindFirst(SessionClaims.Role)?.Value, out var role)
            ? role
            : null;

    private Guid ReadGuid(string claim) =>
        IsAuthenticated && Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(claim)?.Value, out var value) ? value : Guid.Empty;

}