using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxScore",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [assessment].[AssignmentVersions] SET [MaxScore] = 20 WHERE [MaxScore] IS NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "MaxScore",
                schema: "assessment",
                table: "AssignmentVersions",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,2)",
                oldPrecision: 9,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "EvaluationRevisions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    LearnerFeedback = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    GuardianVisibleFeedback = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    TeacherPrivateNote = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    SupersedesEvaluationRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRevisions", x => x.Id);
                    table.UniqueConstraint("UQ_EvaluationRevisions_Organization_Attempt_Id", x => new { x.OrganizationId, x.SubmissionAttemptId, x.Id });
                    table.CheckConstraint("CK_EvaluationRevisions_Correction", "([RevisionNumber] = 1 AND [SupersedesEvaluationRevisionId] IS NULL AND [CorrectionReason] IS NULL) OR ([RevisionNumber] > 1 AND [SupersedesEvaluationRevisionId] IS NOT NULL AND [CorrectionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_EvaluationRevisions_RevisionNumber", "[RevisionNumber] > 0");
                    table.CheckConstraint("CK_EvaluationRevisions_Score", "[Score] >= 0");
                    table.CheckConstraint("CK_EvaluationRevisions_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_EvaluationRevisions_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                        columns: x => new { x.OrganizationId, x.SubmissionAttemptId, x.SupersedesEvaluationRevisionId },
                        principalSchema: "assessment",
                        principalTable: "EvaluationRevisions",
                        principalColumns: new[] { "OrganizationId", "SubmissionAttemptId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRevisions_Memberships_OrganizationId_CreatedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CreatedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationRevisions_SubmissionAttempts_OrganizationId_SubmissionAttemptId",
                        columns: x => new { x.OrganizationId, x.SubmissionAttemptId },
                        principalSchema: "assessment",
                        principalTable: "SubmissionAttempts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeReleases",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeReleases_EvaluationRevisions_OrganizationId_SubmissionAttemptId_EvaluationRevisionId",
                        columns: x => new { x.OrganizationId, x.SubmissionAttemptId, x.EvaluationRevisionId },
                        principalSchema: "assessment",
                        principalTable: "EvaluationRevisions",
                        principalColumns: new[] { "OrganizationId", "SubmissionAttemptId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeReleases_Memberships_OrganizationId_ReleasedByMembershipId",
                        columns: x => new { x.OrganizationId, x.ReleasedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssignmentVersions_MaxScore",
                schema: "assessment",
                table: "AssignmentVersions",
                sql: "[MaxScore] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRevisions_OrganizationId_CreatedByMembershipId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRevisions_OrganizationId_SubmissionAttemptId_SupersedesEvaluationRevisionId",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "SupersedesEvaluationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_EvaluationRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EvaluationRevisions_OneCurrentStatus",
                schema: "assessment",
                table: "EvaluationRevisions",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "Status" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_OrganizationId_ReleasedByMembershipId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "ReleasedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeReleases_OrganizationId_SubmissionAttemptId_EvaluationRevisionId",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "EvaluationRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_GradeReleases_Organization_EvaluationRevision",
                schema: "assessment",
                table: "GradeReleases",
                columns: new[] { "OrganizationId", "EvaluationRevisionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeReleases",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "EvaluationRevisions",
                schema: "assessment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssignmentVersions_MaxScore",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                schema: "assessment",
                table: "AssignmentVersions");
        }
    }
}
