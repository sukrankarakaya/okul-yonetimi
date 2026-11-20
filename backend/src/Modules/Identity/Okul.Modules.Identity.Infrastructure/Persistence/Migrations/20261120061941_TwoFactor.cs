using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "two_factor_last_step",
                schema: "kimlik",
                table: "staff_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_secret",
                schema: "kimlik",
                table: "staff_accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "login_challenges",
                schema: "kimlik",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    client = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    credential_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pending_secret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_challenges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recovery_codes",
                schema: "kimlik",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_login_challenges_tenant_account",
                schema: "kimlik",
                table: "login_challenges",
                columns: new[] { "tenant_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recovery_codes_tenant_account",
                schema: "kimlik",
                table: "recovery_codes",
                columns: new[] { "tenant_id", "account_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_challenges",
                schema: "kimlik");

            migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "kimlik");

            migrationBuilder.DropColumn(
                name: "two_factor_last_step",
                schema: "kimlik",
                table: "staff_accounts");

            migrationBuilder.DropColumn(
                name: "two_factor_secret",
                schema: "kimlik",
                table: "staff_accounts");
        }
    }
}
