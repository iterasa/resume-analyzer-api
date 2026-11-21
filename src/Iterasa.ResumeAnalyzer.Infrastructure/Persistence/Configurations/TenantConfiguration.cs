using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de <see cref="Tenant" /> a <c>tenant</c>.</summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Tenant> builder) {

        builder.ToTable("tenant");
        builder.HasKey(t => t.Id).HasName("pk_tenant");
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(Tenant.MaxNameLength).IsRequired();
        builder.Property(t => t.Country).HasColumnName("country").HasMaxLength(32).IsRequired();
        builder.Property(t => t.PlanCode).HasColumnName("plan_code").HasMaxLength(32).IsRequired();
        builder.Property(t => t.PlanStatus).HasColumnName("plan_status").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(t => t.TrialEndsAtUtc).HasColumnName("trial_ends_at_utc");
        builder.Property(t => t.RetentionDays).HasColumnName("retention_days").IsRequired();
        builder.Property(t => t.AnonymousReviewByDefault).HasColumnName("anonymous_review_by_default").IsRequired();

        builder.ConfigureAuditColumns();

    }

}