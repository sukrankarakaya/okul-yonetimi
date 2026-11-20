using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Parents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Parent_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "parent");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "parent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kvkk_consents",
                schema: "parent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kvkk_consents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sms_codes",
                schema: "parent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sms_codes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_TenantId",
                schema: "parent",
                table: "accounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_TenantId_PhoneNumber",
                schema: "parent",
                table: "accounts",
                columns: new[] { "TenantId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kvkk_consents_TenantId_ParentId",
                schema: "parent",
                table: "kvkk_consents",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_sms_codes_ExpiresAt",
                schema: "parent",
                table: "sms_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_sms_codes_TenantId_ParentId",
                schema: "parent",
                table: "sms_codes",
                columns: new[] { "TenantId", "ParentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "parent");

            migrationBuilder.DropTable(
                name: "kvkk_consents",
                schema: "parent");

            migrationBuilder.DropTable(
                name: "sms_codes",
                schema: "parent");
        }
    }
}
