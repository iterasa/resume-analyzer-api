using Iterasa.ResumeAnalyzer.Infrastructure.Security.DataProtection;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de <see cref="DataProtectionKeyRecord" /> a <c>data_protection_key</c>.</summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKeyRecord> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataProtectionKeyRecord> builder) {

        builder.ToTable("data_protection_key");
        builder.HasKey(k => k.Id).HasName("pk_data_protection_key");
        builder.Property(k => k.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(k => k.FriendlyName).HasColumnName("friendly_name").HasMaxLength(256);
        builder.Property(k => k.Xml).HasColumnName("xml").IsRequired().IsEncrypted(EncryptedFields.DataProtectionKeyXml);
        builder.Property(k => k.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

    }

}