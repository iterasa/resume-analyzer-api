using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Iterasa.ResumeAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Editada a mano (ADR 0015 y 0017):
    /// <list type="bullet">
    /// <item>se detiene si <c>user</c> ya tiene filas: el nombre nuevo va cifrado y solo lo cifra
    /// la aplicación, nunca una migración;</item>
    /// <item><c>Down</c> rellena <c>username</c> con el id de cada usuario antes de su índice único;</item>
    /// <item><c>xmin</c> se retiró de los <c>CreateTable</c>: es una columna de sistema de PostgreSQL
    /// y crearla falla (ver README, «Migraciones»).</item>
    /// </list>
    /// </remarks>
    public partial class TenantsSessionsVacancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseExistingUsers);

            migrationBuilder.DropIndex(
                name: "ix_user_username",
                table: "user");

            migrationBuilder.DropColumn(
                name: "username",
                table: "user");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_verified_at_utc",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failed_sign_in_attempts",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "job_title",
                table: "user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until_utc",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "user",
                type: "text",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "user",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "data_protection_key",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    friendly_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xml = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_key", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_key",
                columns: table => new
                {
                    scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_key", x => new { x.scope, x.key });
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    country = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    plan_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    plan_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    trial_ends_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    anonymous_review_by_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "membership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    anonymous_review = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership", x => x.id);
                    table.ForeignKey(
                        name: "fk_membership_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_membership_user",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_session",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    persistent = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idle_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_session", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_session_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_session_user",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    area = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    modality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    close_reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closes_on = table.Column<DateOnly>(type: "date", nullable: false),
                    opened_on = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    apply_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacancy", x => x.id);
                    table.ForeignKey(
                        name: "fk_vacancy_owner",
                        column: x => x.owner_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacancy_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vacancy_requirement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacancy_requirement", x => x.id);
                    table.ForeignKey(
                        name: "fk_vacancy_requirement_vacancy",
                        column: x => x.vacancy_id,
                        principalTable: "vacancy",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_key_expires_at_utc",
                table: "idempotency_key",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_membership_tenant_user",
                table: "membership",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_membership_user",
                table: "membership",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_session_absolute_expires",
                table: "user_session",
                column: "absolute_expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_user_session_tenant_id",
                table: "user_session",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_session_token_hash",
                table: "user_session",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_session_user",
                table: "user_session",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_apply_token",
                table: "vacancy",
                column: "apply_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vacancy_owner_id",
                table: "vacancy",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_tenant_status_created",
                table: "vacancy",
                columns: new[] { "tenant_id", "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_vacancy_requirement_vacancy",
                table: "vacancy_requirement",
                column: "vacancy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_protection_key");

            migrationBuilder.DropTable(
                name: "idempotency_key");

            migrationBuilder.DropTable(
                name: "membership");

            migrationBuilder.DropTable(
                name: "user_session");

            migrationBuilder.DropTable(
                name: "vacancy_requirement");

            migrationBuilder.DropTable(
                name: "vacancy");

            migrationBuilder.DropTable(
                name: "tenant");

            migrationBuilder.DropColumn(
                name: "email_verified_at_utc",
                table: "user");

            migrationBuilder.DropColumn(
                name: "failed_sign_in_attempts",
                table: "user");

            migrationBuilder.DropColumn(
                name: "job_title",
                table: "user");

            migrationBuilder.DropColumn(
                name: "locked_until_utc",
                table: "user");

            migrationBuilder.DropColumn(
                name: "name",
                table: "user");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "user");

            // Cada usuario recibe un nombre de usuario propio (su id): con el mismo valor para todos,
            // el índice único de abajo fallaría en cuanto hubiera dos.
            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "user",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "user" SET username = id::text;""");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "user",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_username",
                table: "user",
                column: "username",
                unique: true);
        }
    
        private const string RefuseExistingUsers = """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "user") THEN
                    RAISE EXCEPTION 'TenantsSessionsVacancies: "user" has rows. Their names must be encrypted by the application, not by a migration. See docs/runbooks/cifrado-de-campos.md.';
                END IF;
            END $$;
            """;
    }
}
