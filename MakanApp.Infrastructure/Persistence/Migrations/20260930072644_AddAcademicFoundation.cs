using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_OrganizationId",
                schema: "organization",
                table: "Memberships");

            migrationBuilder.EnsureSchema(
                name: "academic");

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Memberships_OrganizationId_Id",
                schema: "organization",
                table: "Memberships",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "AcademicPeriods",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPeriods", x => x.Id);
                    table.UniqueConstraint("UQ_AcademicPeriods_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_AcademicPeriods_DateRange", "[EndDate] > [StartDate]");
                    table.CheckConstraint("CK_AcademicPeriods_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_AcademicPeriods_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.UniqueConstraint("UQ_Courses_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Courses_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Courses_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                    table.UniqueConstraint("UQ_Classes_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Classes_Capacity", "[Capacity] > 0");
                    table.CheckConstraint("CK_Classes_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Classes_AcademicPeriods_OrganizationId_AcademicPeriodId",
                        columns: x => new { x.OrganizationId, x.AcademicPeriodId },
                        principalSchema: "academic",
                        principalTable: "AcademicPeriods",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classes_Courses_OrganizationId_CourseId",
                        columns: x => new { x.OrganizationId, x.CourseId },
                        principalSchema: "academic",
                        principalTable: "Courses",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearnerOrganizationPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnrolledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.CheckConstraint("CK_Enrollments_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Enrollments_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_OrganizationPersons_OrganizationId_LearnerOrganizationPersonId",
                        columns: x => new { x.OrganizationId, x.LearnerOrganizationPersonId },
                        principalSchema: "organization",
                        principalTable: "OrganizationPersons",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAssignments",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAssignments", x => x.Id);
                    table.CheckConstraint("CK_TeacherAssignments_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Memberships_OrganizationId_TeacherMembershipId",
                        columns: x => new { x.OrganizationId, x.TeacherMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Classes_OrganizationId_AcademicPeriodId",
                schema: "academic",
                table: "Classes",
                columns: new[] { "OrganizationId", "AcademicPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_Classes_OrganizationId_CourseId",
                schema: "academic",
                table: "Classes",
                columns: new[] { "OrganizationId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_OrganizationId_LearnerOrganizationPersonId",
                schema: "academic",
                table: "Enrollments",
                columns: new[] { "OrganizationId", "LearnerOrganizationPersonId" });

            migrationBuilder.CreateIndex(
                name: "UX_Enrollments_Active_Organization_Class_Learner",
                schema: "academic",
                table: "Enrollments",
                columns: new[] { "OrganizationId", "ClassId", "LearnerOrganizationPersonId" },
                unique: true,
                filter: "[Status] = 1 AND [EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_OrganizationId_TeacherMembershipId",
                schema: "academic",
                table: "TeacherAssignments",
                columns: new[] { "OrganizationId", "TeacherMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_TeacherAssignments_Active_Organization_Class_Teacher",
                schema: "academic",
                table: "TeacherAssignments",
                columns: new[] { "OrganizationId", "ClassId", "TeacherMembershipId" },
                unique: true,
                filter: "[Status] = 1 AND [EndedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enrollments",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "TeacherAssignments",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Classes",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "AcademicPeriods",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "academic");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Memberships_OrganizationId_Id",
                schema: "organization",
                table: "Memberships");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_OrganizationId",
                schema: "organization",
                table: "Memberships",
                column: "OrganizationId");
        }
    }
}
