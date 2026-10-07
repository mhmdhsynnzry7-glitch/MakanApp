using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamGradingAndRelease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamGradeRevisions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LearnerFeedback = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    GuardianVisibleFeedback = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    EvaluatorPrivateNote = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    SupersedesExamGradeRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamGradeRevisions", x => x.Id);
                    table.UniqueConstraint("UQ_ExamGradeRevisions_Organization_Attempt_Id", x => new { x.OrganizationId, x.ExamAttemptId, x.Id });
                    table.CheckConstraint("CK_ExamGradeRevisions_Correction", "([RevisionNumber] = 1 AND [SupersedesExamGradeRevisionId] IS NULL AND [CorrectionReason] IS NULL) OR ([RevisionNumber] > 1 AND [SupersedesExamGradeRevisionId] IS NOT NULL AND [CorrectionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_ExamGradeRevisions_RevisionNumber", "[RevisionNumber] > 0");
                    table.CheckConstraint("CK_ExamGradeRevisions_Status", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_ExamGradeRevisions_TotalScore", "[TotalScore] >= 0");
                    table.CheckConstraint("CK_ExamGradeRevisions_UpdatedAt", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ExamGradeRevisions_ExamAttempts_OrganizationId_ExamAttemptId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttempts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamGradeRevisions_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.SupersedesExamGradeRevisionId },
                        principalSchema: "assessment",
                        principalTable: "ExamGradeRevisions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamGradeRevisions_Memberships_OrganizationId_CreatedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CreatedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamGradeReleases",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamGradeRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamGradeReleases", x => x.Id);
                    table.CheckConstraint("CK_ExamGradeReleases_RequestHash", "LEN([RequestHash]) = 64");
                    table.ForeignKey(
                        name: "FK_ExamGradeReleases_ExamGradeRevisions_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamGradeRevisionId },
                        principalSchema: "assessment",
                        principalTable: "ExamGradeRevisions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamGradeReleases_Memberships_OrganizationId_ReleasedByMembershipId",
                        columns: x => new { x.OrganizationId, x.ReleasedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestionGrades",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamGradeRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradingMode = table.Column<int>(type: "int", nullable: false),
                    IsAnswered = table.Column<bool>(type: "bit", nullable: false),
                    MaximumScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    AwardedScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    IsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    LearnerFeedback = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    EvaluatorPrivateNote = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    ReviewedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestionGrades", x => x.Id);
                    table.CheckConstraint("CK_ExamQuestionGrades_AwardedScore", "[AwardedScore] >= 0 AND [AwardedScore] <= [MaximumScore]");
                    table.CheckConstraint("CK_ExamQuestionGrades_GradingMode", "[GradingMode] IN (1, 2)");
                    table.CheckConstraint("CK_ExamQuestionGrades_MaximumScore", "[MaximumScore] > 0");
                    table.CheckConstraint("CK_ExamQuestionGrades_ObjectiveReviewed", "[GradingMode] <> 1 OR [IsReviewed] = 1");
                    table.CheckConstraint("CK_ExamQuestionGrades_ReviewState", "([IsReviewed] = 0 AND [ReviewedByMembershipId] IS NULL AND [ReviewedAtUtc] IS NULL) OR ([IsReviewed] = 1 AND [ReviewedByMembershipId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ExamQuestionGrades_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.QuestionVersionId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttemptQuestions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamQuestionGrades_ExamGradeRevisions_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamGradeRevisionId },
                        principalSchema: "assessment",
                        principalTable: "ExamGradeRevisions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamQuestionGrades_Memberships_OrganizationId_ReviewedByMembershipId",
                        columns: x => new { x.OrganizationId, x.ReviewedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_Attempt_ReleasedAtUtc",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_OrganizationId_ExamAttemptId_ExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamGradeRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeReleases_OrganizationId_ReleasedByMembershipId",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ReleasedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeReleases_Attempt_ClientOperation",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeReleases_Organization_Revision",
                schema: "assessment",
                table: "ExamGradeReleases",
                columns: new[] { "OrganizationId", "ExamGradeRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeRevisions_OrganizationId_CreatedByMembershipId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamGradeRevisions_OrganizationId_ExamAttemptId_SupersedesExamGradeRevisionId",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "SupersedesExamGradeRevisionId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_Attempt_RevisionNumber",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_OneMutable",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "UX_ExamGradeRevisions_OneReleased",
                schema: "assessment",
                table: "ExamGradeRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Status" },
                unique: true,
                filter: "[Status] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestionGrades_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestionGrades_OrganizationId_ReviewedByMembershipId",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ReviewedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamQuestionGrades_Revision_AttemptQuestion",
                schema: "assessment",
                table: "ExamQuestionGrades",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamGradeRevisionId", "ExamAttemptQuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamGradeReleases",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ExamQuestionGrades",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ExamGradeRevisions",
                schema: "assessment");
        }
    }
}
