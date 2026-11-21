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

        // Cifrados y sin límite de longitud en la columna: el límite lo valida el dominio sobre
        // el texto. La unicidad del correo la impone su índice ciego (ix_user_email_bidx).
        builder.Property(u => u.Name).HasColumnName("name").IsRequired().IsEncrypted(EncryptedFields.UserName);
        builder.Property(u => u.JobTitle).HasColumnName("job_title").IsEncrypted(EncryptedFields.UserJobTitle);

        builder.OwnsOne(u => u.Email, email => {
            email.Property(e => e.Value).HasColumnName("email").IsRequired().IsEncrypted(EncryptedFields.UserEmail);
        });

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(User.MaxPasswordHashLength);
        builder.Property(u => u.FailedSignInAttempts).HasColumnName("failed_sign_in_attempts").IsRequired();
        builder.Property(u => u.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(u => u.EmailVerifiedAtUtc).HasColumnName("email_verified_at_utc");

        builder.ConfigureAuditColumns();

    }

}