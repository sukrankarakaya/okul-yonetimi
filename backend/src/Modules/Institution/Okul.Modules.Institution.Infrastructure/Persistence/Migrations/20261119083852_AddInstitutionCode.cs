using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.Institution.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                schema: "kurum",
                table: "institutions",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            // Existing rows (development databases) get a code before the unique index: the two sample kurums their
            // seed codes, any other kurum the first 12 hex digits of its id (spec 0003; plan 0003 step 1).
            migrationBuilder.Sql(
                """
                UPDATE kurum.institutions SET code = CASE id
                    WHEN '019a8f00-0000-7000-8000-00000000000a' THEN 'ATATURK-IST'
                    WHEN '019a8f00-0000-7000-8000-00000000000b' THEN 'BILGE'
                    ELSE upper(substr(replace(id::text, '-', ''), 1, 12))
                END
                WHERE code = '';
                """);

            migrationBuilder.CreateIndex(
                name: "ux_institutions_code",
                schema: "kurum",
                table: "institutions",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_institutions_code",
                schema: "kurum",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "code",
                schema: "kurum",
                table: "institutions");
        }
    }
}
