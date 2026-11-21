using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de <see cref="Membership" /> a <c>membership</c>.</summary>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Membership> builder) {

        builder.ToTable("membership");
        builder.HasKey(m => m.Id).HasName("pk_membership");
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Role).HasColumnName("role").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(m => m.AnonymousReview).HasColumnName("anonymous_review").IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_tenant");
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_user");

        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique().HasDatabaseName("ix_membership_tenant_user");
        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_membership_user");

        builder.ConfigureAuditColumns();

    }

}