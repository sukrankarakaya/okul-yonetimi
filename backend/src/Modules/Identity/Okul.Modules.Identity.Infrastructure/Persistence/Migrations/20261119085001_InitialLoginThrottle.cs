using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Identity.Infrastructure.Persistence.Migrations.LoginThrottle
{
    /// <inheritdoc />
    public partial class InitialLoginThrottle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kimlik");

            migrationBuilder.CreateTable(
                name: "login_throttles",
                schema: "kimlik",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    failure_count = table.Column<int>(type: "integer", nullable: false),
                    last_failure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_throttles", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_throttles",
                schema: "kimlik");
        }
    }
}
