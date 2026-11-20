using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Identity.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kimlik");

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "kimlik",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    current_refresh_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    previous_refresh_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_accounts",
                schema: "kimlik",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    roles = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_accounts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_tenant_account",
                schema: "kimlik",
                table: "sessions",
                columns: new[] { "tenant_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_staff_accounts_tenant_email",
                schema: "kimlik",
                table: "staff_accounts",
                columns: new[] { "tenant_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sessions",
                schema: "kimlik");

            migrationBuilder.DropTable(
                name: "staff_accounts",
                schema: "kimlik");
        }
    }
}
