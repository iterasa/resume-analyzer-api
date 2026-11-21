using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de <see cref="UserSession" /> a <c>user_session</c>.</summary>
internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserSession> builder) {

        builder.ToTable("user_session");
        builder.HasKey(s => s.Id).HasName("pk_user_session");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.TokenHash).HasColumnName("token_hash").HasMaxLength(SessionTokens.HashSize).IsRequired();
        builder.Property(s => s.Persistent).HasColumnName("persistent").IsRequired();
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(s => s.LastSeenAtUtc).HasColumnName("last_seen_at_utc").IsRequired();
        builder.Property(s => s.IdleExpiresAtUtc).HasColumnName("idle_expires_at_utc").IsRequired();
        builder.Property(s => s.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").IsRequired();
        builder.Property(s => s.RevokedAtUtc).HasColumnName("revoked_at_utc");

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_user_session_user");
        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_user_session_tenant");

        builder.HasIndex(s => s.TokenHash).IsUnique().HasDatabaseName("ix_user_session_token_hash");
        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_user_session_user");
        builder.HasIndex(s => s.AbsoluteExpiresAtUtc).HasDatabaseName("ix_user_session_absolute_expires");

    }

}