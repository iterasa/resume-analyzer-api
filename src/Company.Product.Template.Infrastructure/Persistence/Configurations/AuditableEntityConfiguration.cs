using Company.Product.Template.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Product.Template.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo común de las columnas de auditoría y borrado lógico.
/// Toda entidad persistida debe invocarlo para respetar la convención.
/// </summary>
internal static class AuditableEntityConfiguration {

    /// <summary>
    /// Mapea <c>created_at_utc</c>, <c>created_by</c>, <c>updated_at_utc</c>, <c>updated_by</c> e <c>is_enabled</c>.
    /// El sufijo <c>_utc</c> documenta en la propia columna que el instante no lleva zona local.
    /// </summary>
    /// <typeparam name="TEntity">Entidad auditable.</typeparam>
    /// <param name="builder">Constructor de la entidad.</param>
    /// <returns>El mismo constructor para encadenamiento.</returns>
    public static EntityTypeBuilder<TEntity> ConfigureAuditColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity {

        builder.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(entity => entity.UpdatedBy)
            .HasColumnName("updated_by")
            .IsRequired();

        builder.Property(entity => entity.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired();

        return builder;

    }

}