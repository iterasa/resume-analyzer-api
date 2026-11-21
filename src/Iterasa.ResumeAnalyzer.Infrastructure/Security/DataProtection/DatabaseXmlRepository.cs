using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Security.DataProtection;

/// <summary>
/// Anillo de llaves de Data Protection en PostgreSQL: todas las instancias firman y validan los
/// tokens antiforgery con las mismas llaves, y sobreviven a un reinicio (ADR 0017).
/// </summary>
/// <remarks>
/// El XML de cada llave va en una columna cifrada (<see cref="FieldEncryption.EncryptedFields.DataProtectionKeyXml" />):
/// quien lea la base no puede falsificar tokens. Data Protection avisa al crear una llave de que
/// no tiene «XML encryptor»: es esperado, el cifrado lo hace la columna.
/// </remarks>
internal sealed class DatabaseXmlRepository(IServiceScopeFactory scopeFactory, TimeProvider clock) : IXmlRepository {

    public IReadOnlyCollection<XElement> GetAllElements() {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return [.. dbContext.DataProtectionKeys.AsNoTracking().Select(key => key.Xml).AsEnumerable().Select(XElement.Parse)];
    }

    public void StoreElement(XElement element, string friendlyName) {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.DataProtectionKeys.Add(new DataProtectionKeyRecord {
            Id = Guid.NewGuid(),
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
            CreatedAtUtc = clock.GetUtcNow(),
        });
        dbContext.SaveChanges();
    }

}