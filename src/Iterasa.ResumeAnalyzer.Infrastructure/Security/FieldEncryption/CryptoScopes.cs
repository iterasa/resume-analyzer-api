namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;

/// <summary>
/// Ámbitos de llaves. Cada empresa (tenant) es su propio ámbito, identificado por su id; lo que
/// no pertenece a una empresa (los usuarios, que pueden estar en varias) va en el ámbito de
/// plataforma, con sus propias llaves.
/// </summary>
internal static class CryptoScopes {

    /// <summary>Ámbito de plataforma. No coincide con ningún id de empresa (los genera <see cref="Guid.NewGuid" />).</summary>
    public static readonly Guid Platform = new("00000000-0000-0000-0000-00000000c0de");

}

/// <summary>Dónde vive la llave de un campo.</summary>
internal enum CryptoScopeKind {

    /// <summary>Llaves de plataforma: datos que no son de una sola empresa.</summary>
    Platform,

    /// <summary>Llaves de la empresa de la operación (<see cref="TenantCryptoScope" />).</summary>
    Tenant,

}

/// <summary>
/// Empresa de la operación en curso, para cifrar y descifrar sus campos.
/// </summary>
/// <remarks>
/// En una petición la fija el middleware de empresa; en un proceso sin petición (jobs,
/// despachadores, herramientas) se abre explícitamente con la empresa de la propia fila:
/// <c>using (TenantCryptoScope.Enter(tenantId)) { … }</c>. Sin ámbito, un campo de empresa
/// no se cifra ni se descifra: nunca se cae a una llave global.
/// </remarks>
internal static class TenantCryptoScope {

    private static readonly AsyncLocal<Guid?> current = new();

    /// <summary>Empresa vigente, o <see langword="null" /> fuera de un ámbito.</summary>
    public static Guid? Current => current.Value;

    /// <summary>Abre el ámbito de una empresa hasta que se libera el resultado; restaura el anterior.</summary>
    public static IDisposable Enter(Guid tenantId) {
        if (tenantId == Guid.Empty || tenantId == CryptoScopes.Platform) {
            throw new ArgumentException("A tenant crypto scope needs a real tenant id.", nameof(tenantId));
        }
        var previous = current.Value;
        current.Value = tenantId;
        return new Restore(previous);
    }

    /// <summary>Ámbito de un campo: el de plataforma o el de la empresa vigente.</summary>
    /// <exception cref="InvalidOperationException">Campo de empresa sin ámbito abierto.</exception>
    public static Guid Resolve(EncryptedField field) => field.Scope switch {
        CryptoScopeKind.Platform => CryptoScopes.Platform,
        CryptoScopeKind.Tenant => current.Value
            ?? throw new InvalidOperationException(
                $"Field {field.Name} belongs to a tenant, but no tenant crypto scope is open. Open one with TenantCryptoScope.Enter(tenantId)."),
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    private sealed class Restore(Guid? previous) : IDisposable {
        private bool disposed;
        public void Dispose() {
            if (disposed) {
                return;
            }
            disposed = true;
            current.Value = previous;
        }
    }

}