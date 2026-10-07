using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionVersions_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "OrganizationId", "ExamVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Exams_Organization_Class_Id",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "ClassId", "Id" });

            migrationBuilder.CreateTable(
                name: "ExamAttempts",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EffectiveDeadlineUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAttempts", x => x.Id);
                    table.UniqueConstraint("UQ_ExamAttempts_Organization_ExamVersion_Id", x => new { x.OrganizationId, x.ExamVersionId, x.Id });
                    table.CheckConstraint("CK_ExamAttempts_AttemptNumber", "[AttemptNumber] > 0");
                    table.CheckConstraint("CK_ExamAttempts_Deadline", "[EffectiveDeadlineUtc] > [StartedAtUtc]");
                    table.CheckConstraint("CK_ExamAttempts_ExpirationState", "([Status] = 1 AND [ExpiredAtUtc] IS NULL) OR ([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ExamAttempts_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ExamAttempts_Enrollments_OrganizationId_ClassId_EnrollmentId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.EnrollmentId },
                        principalSchema: "academic",
                        principalTable: "Enrollments",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_ExamVersions_OrganizationId_ExamId_ExamVersionId",
                        columns: x => new { x.OrganizationId, x.ExamId, x.ExamVersionId },
                        principalSchema: "assessment",
                        principalTable: "ExamVersions",
                        principalColumns: new[] { "OrganizationId", "ExamId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_Exams_OrganizationId_ClassId_ExamId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.ExamId },
                        principalSchema: "assessment",
                        principalTable: "Exams",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamAttemptQuestions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAttemptQuestions", x => x.Id);
                    table.CheckConstraint("CK_ExamAttemptQuestions_DisplayOrder", "[DisplayOrder] > 0");
                    table.ForeignKey(
                        name: "FK_ExamAttemptQuestions_ExamAttempts_OrganizationId_ExamVersionId_ExamAttemptId",
                        columns: x => new { x.OrganizationId, x.ExamVersionId, x.ExamAttemptId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttempts",
                        principalColumns: new[] { "OrganizationId", "ExamVersionId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamAttemptQuestions_QuestionVersions_OrganizationId_ExamVersionId_QuestionVersionId",
                        columns: x => new { x.OrganizationId, x.ExamVersionId, x.QuestionVersionId },
                        principalSchema: "assessment",
                        principalTable: "QuestionVersions",
                        principalColumns: new[] { "OrganizationId", "ExamVersionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_ExamAttemptId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamVersionId", "ExamAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamVersionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamVersionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttemptQuestions_Organization_Attempt_Question",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "QuestionVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_OrganizationId_ClassId_EnrollmentId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ClassId", "EnrollmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_OrganizationId_ClassId_ExamId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ClassId", "ExamId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_OrganizationId_ExamId_ExamVersionId",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "ExamVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Enrollment_ClientOperation",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "EnrollmentId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Active",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "EnrollmentId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempts_Organization_Exam_Enrollment_Number",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "ExamId", "EnrollmentId", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamAttemptQuestions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ExamAttempts",
                schema: "assessment");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionVersions_Organization_ExamVersion_Id",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Exams_Organization_Class_Id",
                schema: "assessment",
                table: "Exams");
        }
    }
}
