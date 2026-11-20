using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Enrollment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuardianLoginInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kayit");

            migrationBuilder.CreateTable(
                name: "guardian_login_codes",
                schema: "kayit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pair_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardian_login_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guardian_login_throttles",
                schema: "kayit",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    last_failure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    request_window_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    request_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardian_login_throttles", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guardian_login_codes_pair",
                schema: "kayit",
                table: "guardian_login_codes",
                columns: new[] { "pair_key", "issued_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guardian_login_codes",
                schema: "kayit");

            migrationBuilder.DropTable(
                name: "guardian_login_throttles",
                schema: "kayit");
        }
    }
}
