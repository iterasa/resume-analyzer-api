using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Web;

/// <summary>
/// Lee la identidad de la petición en curso. Sin autenticación configurada, toda petición
/// es anónima.
/// </summary>
internal sealed class CurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal {

    /// <inheritdoc />
    public Guid UserId {
        get {
            var raw = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            return Guid.TryParse(raw, out var value) ? value : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

}