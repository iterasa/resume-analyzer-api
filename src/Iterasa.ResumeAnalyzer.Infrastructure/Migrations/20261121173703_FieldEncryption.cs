using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iterasa.ResumeAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Editada a mano (ADR 0015). Una migración no puede cifrar: los datos solo se cifran
    /// con el código de la aplicación. Por eso se detiene si <c>user</c> ya tiene filas, en
    /// lugar de dejar correos en claro donde se espera un criptograma, y las columnas del
    /// índice ciego no llevan valor por defecto (uno vacío chocaría en el índice único).
    /// </remarks>
    public partial class FieldEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseExistingUsers);

            migrationBuilder.DropIndex(
                name: "ix_user_email",
                table: "user");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "user",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<byte[]>(
                name: "email_bidx",
                table: "user",
                type: "bytea",
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "email_bidx_v",
                table: "user",
                type: "integer",
                nullable: false);

            migrationBuilder.CreateTable(
                name: "encryption_key",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    key_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    wrapped_key = table.Column<string>(type: "text", nullable: false),
                    kek_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status_changed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encryption_key", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_email_bidx",
                table: "user",
                column: "email_bidx",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_encryption_key_key_id",
                table: "encryption_key",
                column: "key_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_encryption_key_one_active",
                table: "encryption_key",
                columns: new[] { "scope_id", "purpose" },
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_encryption_key_scope_purpose_version",
                table: "encryption_key",
                columns: new[] { "scope_id", "purpose", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir dejaría criptogramas en una columna que espera correos en claro.
            migrationBuilder.Sql(RefuseExistingUsers);

            migrationBuilder.DropTable(
                name: "encryption_key");

            migrationBuilder.DropIndex(
                name: "ix_user_email_bidx",
                table: "user");

            migrationBuilder.DropColumn(
                name: "email_bidx",
                table: "user");

            migrationBuilder.DropColumn(
                name: "email_bidx_v",
                table: "user");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "user",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "ix_user_email",
                table: "user",
                column: "email",
                unique: true);
        }

        private const string RefuseExistingUsers = """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "user") THEN
                    RAISE EXCEPTION 'FieldEncryption: "user" has rows. Their emails must be (re)encrypted by the application, not by a migration. See docs/runbooks/cifrado-de-campos.md.';
                END IF;
            END $$;
            """;
    }
}
