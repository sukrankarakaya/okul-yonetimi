using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Institution.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInstitution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kurum");

            migrationBuilder.CreateTable(
                name: "institutions",
                schema: "kurum",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr-TR-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "campuses",
                schema: "kurum",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "tr-TR-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campuses", x => x.id);
                    table.ForeignKey(
                        name: "FK_campuses_institutions_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "kurum",
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campuses_tenant_id",
                schema: "kurum",
                table: "campuses",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campuses",
                schema: "kurum");

            migrationBuilder.DropTable(
                name: "institutions",
                schema: "kurum");
        }
    }
}
