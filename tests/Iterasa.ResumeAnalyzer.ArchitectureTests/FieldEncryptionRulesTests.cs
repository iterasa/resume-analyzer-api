using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Persistence;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Iterasa.ResumeAnalyzer.ArchitectureTests;

/// <summary>
/// Reglas del cifrado de campos (ADR 0015) que un cambio con prisa rompería sin que una
/// prueba funcional lo notara.
/// </summary>
public sealed class FieldEncryptionRulesTests {

    private const string CryptoNamespace = "Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption";

    private static readonly Microsoft.EntityFrameworkCore.Metadata.IModel Model = BuildModel();

    [Fact]
    public void The_Model_And_The_Inventory_Declare_The_Same_Encrypted_Fields() {
        var inModel = Model.GetEntityTypes()
            .SelectMany(type => type.GetProperties())
            .Select(property => property.FindEncryptedField()?.Name)
            .OfType<string>();

        inModel.Should().BeEquivalentTo(EncryptedFields.All.Select(field => field.Name));
    }

    [Fact]
    public void No_Encrypted_Column_Has_A_Length_Limit() {
        foreach (var property in EncryptedProperties()) {
            property.GetMaxLength().Should().BeNull($"{property.FindEncryptedField()!.Name} stores a ciphertext longer than the value");
        }
    }

    [Fact]
    public void Every_Searchable_Field_Has_Its_Blind_Index_And_A_Database_Index() {
        foreach (var property in EncryptedProperties().Where(p => p.FindEncryptedField()!.Search is not null)) {
            var field = property.FindEncryptedField()!;
            var hash = property.DeclaringType.FindProperty(field.BlindIndexProperty);
            hash.Should().NotBeNull();
            property.DeclaringType.FindProperty(field.BlindIndexVersionProperty).Should().NotBeNull();
            ((Microsoft.EntityFrameworkCore.Metadata.IEntityType)property.DeclaringType).GetIndexes()
                .Should().Contain(index => index.Properties.Contains(hash!) && index.IsUnique == field.Search!.Unique);
        }
    }

    [Fact]
    public void Only_The_Central_Library_Uses_Cryptographic_Primitives() {

        // Comprobación de la propia regla: si no viera la dependencia dentro de la librería,
        // tampoco la vería fuera.
        Types.InAssembly(Assembly.Load("Iterasa.ResumeAnalyzer.Infrastructure"))
            .That().HaveDependencyOn("System.Security.Cryptography.AesGcm")
            .GetTypes().Should().NotBeEmpty();

        foreach (var assembly in new[] { "Iterasa.ResumeAnalyzer.Infrastructure", "Iterasa.ResumeAnalyzer.Application", "Iterasa.ResumeAnalyzer.Api", "Iterasa.ResumeAnalyzer.Domain" }) {
            var result = Types.InAssembly(Assembly.Load(assembly))
                .That().DoNotResideInNamespace(CryptoNamespace)
                .ShouldNot().HaveDependencyOnAny(
                    "System.Security.Cryptography.AesGcm",
                    "System.Security.Cryptography.AesCcm",
                    "System.Security.Cryptography.Aes",
                    "System.Security.Cryptography.HMACSHA256",
                    "System.Security.Cryptography.ChaCha20Poly1305",
                    "Amazon.KeyManagementService")
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"{assembly} must use the field encryption library, not its own crypto: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    private static IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IProperty> EncryptedProperties() =>
        Model.GetEntityTypes().SelectMany(type => type.GetProperties()).Where(property => property.FindEncryptedField() is not null);

    private static Microsoft.EntityFrameworkCore.Metadata.IModel BuildModel() {
        // Construir el modelo no abre conexión.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Port=1;Database=model").Options;
        using var context = new AppDbContext(options, EfFieldEncryption.DesignTime);
        return context.Model;
    }

}