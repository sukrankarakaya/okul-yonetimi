using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Okul.Modules.School.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "okul");

            migrationBuilder.CreateTable(
                name: "announcements",
                schema: "okul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    ReadCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                schema: "okul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GuardianNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "class_groups",
                schema: "okul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, collation: "tr-TR-x-icu"),
                    GradeLevel = table.Column<int>(type: "integer", nullable: false),
                    TeacherName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guardian_messages",
                schema: "okul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AiAssisted = table.Column<bool>(type: "boolean", nullable: false),
                    SentBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardian_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "installments",
                schema: "okul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountKurus = table.Column<long>(type: "bigint", nullable: false),
                    PaidKurus = table.Column<long>(type: "bigint", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceiptNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_installments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "students",
                schema: "okul",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "tr-TR-x-icu"),
                    LastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "tr-TR-x-icu"),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HealthNote = table.Column<string>(type: "text", nullable: true),
                    Allergies = table.Column<string>(type: "text", nullable: true),
                    PhotoConsent = table.Column<bool>(type: "boolean", nullable: false),
                    GuardianName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    GuardianRelation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GuardianPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorizedPickup = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_TenantId_CreatedAt",
                schema: "okul",
                table: "announcements",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_TenantId_Date",
                schema: "okul",
                table: "attendance_records",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_TenantId_StudentId_Date",
                schema: "okul",
                table: "attendance_records",
                columns: new[] { "TenantId", "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_groups_TenantId",
                schema: "okul",
                table: "class_groups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_guardian_messages_TenantId_SentAt",
                schema: "okul",
                table: "guardian_messages",
                columns: new[] { "TenantId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_installments_TenantId_DueDate",
                schema: "okul",
                table: "installments",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_installments_TenantId_StudentId",
                schema: "okul",
                table: "installments",
                columns: new[] { "TenantId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_students_TenantId_ClassGroupId",
                schema: "okul",
                table: "students",
                columns: new[] { "TenantId", "ClassGroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements",
                schema: "okul");

            migrationBuilder.DropTable(
                name: "attendance_records",
                schema: "okul");

            migrationBuilder.DropTable(
                name: "class_groups",
                schema: "okul");

            migrationBuilder.DropTable(
                name: "guardian_messages",
                schema: "okul");

            migrationBuilder.DropTable(
                name: "installments",
                schema: "okul");

            migrationBuilder.DropTable(
                name: "students",
                schema: "okul");
        }
    }
}
