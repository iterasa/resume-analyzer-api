using Company.Product.Template.Domain.Users;
using Company.Product.Template.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Product.Template.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF de <see cref="User" /> a la tabla <c>user</c>.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User> {

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder) {

        builder.ToTable("user");
        builder.HasKey(u => u.Id).HasName("pk_user");
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(User.MaxUsernameLength).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique().HasDatabaseName("ix_user_username");

        builder.OwnsOne(u => u.Email, email => {
            email.Property(e => e.Value).HasColumnName("email").HasMaxLength(Email.MaxLength).IsRequired();
            email.HasIndex(e => e.Value).IsUnique().HasDatabaseName("ix_user_email");
        });

        builder.ConfigureAuditColumns();

    }
}