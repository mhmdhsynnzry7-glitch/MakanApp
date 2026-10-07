using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamFinalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_ExpirationState",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_Status",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.AddColumn<Guid>(
                name: "FinalizeClientOperationId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalizeRequestHash",
                schema: "assessment",
                table: "ExamAttempts",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FinalizedAnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAtUtc",
                schema: "assessment",
                table: "ExamAttempts",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalizedBySessionId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamFinalAnswers",
                schema: "assessment",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamFinalAnswers", x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId });
                    table.ForeignKey(
                        name: "FK_ExamFinalAnswers_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.AnswerRevisionId },
                        principalSchema: "assessment",
                        principalTable: "AnswerRevisions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamFinalAnswers_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.QuestionVersionId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttemptQuestions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamFinalAnswers_ExamAttempts_OrganizationId_ExamAttemptId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttempts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_FinalizedBySessionId",
                schema: "assessment",
                table: "ExamAttempts",
                column: "FinalizedBySessionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_ExpirationState",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([Status] IN (1, 3) AND [ExpiredAtUtc] IS NULL) OR ([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizationState",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([Status] = 3 AND [FinalizedAtUtc] IS NOT NULL AND [FinalizedAnswerSetVersion] IS NOT NULL AND [FinalizeClientOperationId] IS NOT NULL AND [FinalizeRequestHash] IS NOT NULL AND [FinalizedBySessionId] IS NOT NULL) OR ([Status] <> 3 AND [FinalizedAtUtc] IS NULL AND [FinalizedAnswerSetVersion] IS NULL AND [FinalizeClientOperationId] IS NULL AND [FinalizeRequestHash] IS NULL AND [FinalizedBySessionId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizedAnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[FinalizedAnswerSetVersion] IS NULL OR ([FinalizedAnswerSetVersion] >= 0 AND [FinalizedAnswerSetVersion] = [AnswerSetVersion])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizedBeforeDeadline",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[FinalizedAtUtc] IS NULL OR [FinalizedAtUtc] < [EffectiveDeadlineUtc]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_FinalizeRequestHash",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[FinalizeRequestHash] IS NULL OR LEN([FinalizeRequestHash]) = 64");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_Status",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamFinalAnswers_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "ExamFinalAnswers",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_UserSessions_FinalizedBySessionId",
                schema: "assessment",
                table: "ExamAttempts",
                column: "FinalizedBySessionId",
                principalSchema: "identity",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_UserSessions_FinalizedBySessionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropTable(
                name: "ExamFinalAnswers",
                schema: "assessment");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_FinalizedBySessionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_ExpirationState",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizationState",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizedAnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizedBeforeDeadline",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_FinalizeRequestHash",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_Status",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "FinalizeClientOperationId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "FinalizeRequestHash",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "FinalizedAnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "FinalizedAtUtc",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "FinalizedBySessionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_ExpirationState",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([Status] = 1 AND [ExpiredAtUtc] IS NULL) OR ([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_Status",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[Status] IN (1, 2)");
        }
    }
}
