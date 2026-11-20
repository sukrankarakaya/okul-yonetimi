using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Enrollment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrollmentInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kayit");

            migrationBuilder.CreateTable(
                name: "guardians",
                schema: "kayit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    account_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardians", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kvkk_consents",
                schema: "kayit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kvkk_consents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_guardians_tenant_phone",
                schema: "kayit",
                table: "guardians",
                columns: new[] { "tenant_id", "phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kvkk_consents_guardian",
                schema: "kayit",
                table: "kvkk_consents",
                columns: new[] { "tenant_id", "guardian_id", "accepted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guardians",
                schema: "kayit");

            migrationBuilder.DropTable(
                name: "kvkk_consents",
                schema: "kayit");
        }
    }
}
