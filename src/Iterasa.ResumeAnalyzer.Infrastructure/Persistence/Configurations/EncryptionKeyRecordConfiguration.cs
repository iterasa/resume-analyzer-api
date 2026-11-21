using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo de <see cref="EncryptionKeyRecord" /> a <c>encryption_key</c>.
/// </summary>
internal sealed class EncryptionKeyRecordConfiguration : IEntityTypeConfiguration<EncryptionKeyRecord> {

    public void Configure(EntityTypeBuilder<EncryptionKeyRecord> builder) {

        builder.ToTable("encryption_key");
        builder.HasKey(key => key.Id).HasName("pk_encryption_key");
        builder.Property(key => key.Id).HasColumnName("id");

        builder.Property(key => key.ScopeId).HasColumnName("scope_id").IsRequired();
        builder.Property(key => key.Purpose).HasColumnName("purpose").HasMaxLength(3).IsRequired()
            .HasConversion(purpose => purpose.ToCode(), code => KeyCodes.ToKeyPurpose(code));
        builder.Property(key => key.Version).HasColumnName("version").IsRequired();
        builder.Property(key => key.KeyId).HasColumnName("key_id").HasMaxLength(16).IsRequired();
        builder.Property(key => key.WrappedKey).HasColumnName("wrapped_key").IsRequired();
        builder.Property(key => key.KekId).HasColumnName("kek_id").HasMaxLength(2048).IsRequired();
        builder.Property(key => key.Status).HasColumnName("status").HasMaxLength(16).IsRequired()
            .HasConversion(status => status.ToCode(), code => KeyCodes.ToKeyStatus(code));
        builder.Property(key => key.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(key => key.StatusChangedAtUtc).HasColumnName("status_changed_at_utc").IsRequired();

        builder.HasIndex(key => key.KeyId).IsUnique().HasDatabaseName("ix_encryption_key_key_id");
        builder.HasIndex(key => new { key.ScopeId, key.Purpose, key.Version }).IsUnique()
            .HasDatabaseName("ix_encryption_key_scope_purpose_version");

        // Una sola llave activa por ámbito y propósito: lo impone la base, no solo el código.
        builder.HasIndex(key => new { key.ScopeId, key.Purpose }).IsUnique()
            .HasFilter("status = 'active'")
            .HasDatabaseName("ix_encryption_key_one_active");

    }

}