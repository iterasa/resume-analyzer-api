using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF de <see cref="IdempotencyKey" /> a la tabla <c>idempotency_key</c>.
/// </summary>
/// <remarks>
/// La clave primaria compuesta (ámbito, clave) es la que decide la carrera entre dos
/// peticiones simultáneas con la misma clave: la segunda inserción falla y se deshace.
/// </remarks>
internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder) {

        builder.ToTable("idempotency_key");
        builder.HasKey(k => new { k.Scope, k.Key }).HasName("pk_idempotency_key");

        builder.Property(k => k.Scope).HasColumnName("scope").HasMaxLength(IdempotencyKey.MaxScopeLength).IsRequired();
        builder.Property(k => k.Key).HasColumnName("key").HasMaxLength(IdempotencyKey.MaxKeyLength).IsRequired();
        builder.Property(k => k.RequestHash).HasColumnName("request_hash").HasMaxLength(IdempotencyKey.MaxRequestHashLength).IsRequired();
        builder.Property(k => k.StatusCode).HasColumnName("status_code").IsRequired();
        builder.Property(k => k.ContentType).HasColumnName("content_type").HasMaxLength(IdempotencyKey.MaxContentTypeLength);
        // Cifrada con las llaves de la empresa: la respuesta puede llevar datos personales.
        builder.Property(k => k.ResponseBody).HasColumnName("response_body").IsEncrypted(EncryptedFields.IdempotencyResponseBody);
        builder.Property(k => k.Location).HasColumnName("location").HasMaxLength(IdempotencyKey.MaxLocationLength);
        builder.Property(k => k.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(k => k.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();

        builder.HasIndex(k => k.ExpiresAtUtc).HasDatabaseName("ix_idempotency_key_expires_at_utc");

    }

}