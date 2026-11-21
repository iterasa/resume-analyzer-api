using Iterasa.ResumeAnalyzer.Application.Abstractions;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Iterasa.ResumeAnalyzer.Shared.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Tenancy;

/// <summary>Abre la empresa de la operación: filtros de consulta y llaves de cifrado (ADR 0018).</summary>
internal sealed class TenantScope : ITenantScope {

    /// <inheritdoc />
    public IDisposable Enter(Guid tenantId) => TenantCryptoScope.Enter(tenantId);

}

/// <summary>Contexto de empresa por petición.</summary>
public static class TenantContextExtensions {

    /// <summary>
    /// Abre, para el resto de la petición, la empresa activa de la sesión. Va después de la
    /// autenticación. Sin sesión no se abre ninguna: no se ve ni se escribe nada de una empresa.
    /// </summary>
    /// <param name="app">Pipeline de la aplicación.</param>
    /// <returns>El mismo pipeline para encadenamiento.</returns>
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.Use(async (context, next) => {

            if (context.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(context.User.FindFirst(SessionClaims.TenantId)?.Value, out var tenantId)
                && tenantId != Guid.Empty) {
                using (TenantCryptoScope.Enter(tenantId)) {
                    await next(context);
                }
                return;
            }

            await next(context);

        });

}