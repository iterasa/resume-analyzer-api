using Iterasa.ResumeAnalyzer.Domain.Users;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Iterasa.ResumeAnalyzer.Infrastructure.Persistence.Configurations;

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

        // Cifrado y sin límite de longitud en la columna: el límite lo valida el dominio sobre el
        // texto. La unicidad la impone el índice ciego (ix_user_email_bidx), no la columna.
        builder.OwnsOne(u => u.Email, email => {
            email.Property(e => e.Value).HasColumnName("email").IsRequired().IsEncrypted(EncryptedFields.UserEmail);
        });

        builder.ConfigureAuditColumns();

    }
}