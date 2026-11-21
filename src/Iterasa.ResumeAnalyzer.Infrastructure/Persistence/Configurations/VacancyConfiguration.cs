using Iterasa.ResumeAnalyzer.Domain.Tenants;
using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de <see cref="Vacancy" /> a <c>vacancy</c> y de sus requisitos a <c>vacancy_requirement</c>.</summary>
internal sealed class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>, IEntityTypeConfiguration<Requirement> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Vacancy> builder) {

        builder.ToTable("vacancy");
        builder.HasKey(v => v.Id).HasName("pk_vacancy");
        builder.Property(v => v.Id).HasColumnName("id");

        builder.Property(v => v.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(v => v.Title).HasColumnName("title").HasMaxLength(Vacancy.MaxTitleLength).IsRequired();
        builder.Property(v => v.Area).HasColumnName("area").HasMaxLength(80).IsRequired();
        builder.Property(v => v.Modality).HasColumnName("modality").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(v => v.Priority).HasColumnName("priority").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(v => v.Status).HasColumnName("status").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(v => v.CloseReason).HasColumnName("close_reason").HasMaxLength(16).HasConversion<string>();
        builder.Property(v => v.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(v => v.ClosesOn).HasColumnName("closes_on").IsRequired();
        builder.Property(v => v.OpenedOn).HasColumnName("opened_on").IsRequired();
        builder.Property(v => v.ClosedOn).HasColumnName("closed_on");
        builder.Property(v => v.ApplyToken).HasColumnName("apply_token").HasMaxLength(Vacancy.MaxApplyTokenLength).IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(v => v.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vacancy_tenant");
        builder.HasOne<User>().WithMany().HasForeignKey(v => v.OwnerId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_vacancy_owner");

        builder.HasMany(v => v.Requirements).WithOne().HasForeignKey(r => r.VacancyId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_vacancy_requirement_vacancy");
        builder.Navigation(v => v.Requirements).HasField("requirements").UsePropertyAccessMode(PropertyAccessMode.Field);

        // El formulario público llega por el token: único en toda la base, no solo en la empresa.
        builder.HasIndex(v => v.ApplyToken).IsUnique().HasDatabaseName("ix_vacancy_apply_token");
        builder.HasIndex(v => new { v.TenantId, v.Status, v.CreatedAtUtc }).HasDatabaseName("ix_vacancy_tenant_status_created");

        builder.ConfigureAuditColumns();

    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Requirement> builder) {

        builder.ToTable("vacancy_requirement");
        builder.HasKey(r => r.Id).HasName("pk_vacancy_requirement");
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.VacancyId).HasColumnName("vacancy_id").IsRequired();
        builder.Property(r => r.Text).HasColumnName("text").HasMaxLength(Requirement.MaxTextLength).IsRequired();
        builder.Property(r => r.Kind).HasColumnName("kind").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(r => r.Weight).HasColumnName("weight").IsRequired();
        builder.Property(r => r.Position).HasColumnName("position").IsRequired();

        builder.HasIndex(r => r.VacancyId).HasDatabaseName("ix_vacancy_requirement_vacancy");

    }

}