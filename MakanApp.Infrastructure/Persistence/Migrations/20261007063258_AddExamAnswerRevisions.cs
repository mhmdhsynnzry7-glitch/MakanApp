using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAnswerRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WriteLeaseAcquiredAtUtc",
                schema: "assessment",
                table: "ExamAttempts",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WriteLeaseVersion",
                schema: "assessment",
                table: "ExamAttempts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WriterSessionId",
                schema: "assessment",
                table: "ExamAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(
                "UPDATE [assessment].[ExamAttempts] SET [AnswerSetVersion] = 0, [WriteLeaseVersion] = 0 WHERE [AnswerSetVersion] IS NULL OR [WriteLeaseVersion] IS NULL");

            migrationBuilder.AlterColumn<long>(
                name: "AnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "WriteLeaseVersion",
                schema: "assessment",
                table: "ExamAttempts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_QuestionOptions_Organization_QuestionVersion_Id",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_Id",
                schema: "assessment",
                table: "ExamAttempts",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" });

            migrationBuilder.CreateTable(
                name: "AnswerRevisions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    AnswerType = table.Column<int>(type: "int", nullable: false),
                    SelectedOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TextAnswer = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersedesAnswerRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedWriteLeaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedAnswerSetVersion = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerRevisions", x => x.Id);
                    table.UniqueConstraint("UQ_AnswerRevisions_Organization_Attempt_Question_Id", x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.Id });
                    table.CheckConstraint("CK_AnswerRevisions_AnswerSetVersion", "[AcceptedAnswerSetVersion] > 0");
                    table.CheckConstraint("CK_AnswerRevisions_AnswerShape", "([AnswerType] = 1 AND [SelectedOptionId] IS NOT NULL AND [TextAnswer] IS NULL) OR ([AnswerType] = 2 AND [SelectedOptionId] IS NULL AND [TextAnswer] IS NOT NULL)");
                    table.CheckConstraint("CK_AnswerRevisions_AnswerType", "[AnswerType] IN (1, 2)");
                    table.CheckConstraint("CK_AnswerRevisions_RequestHash", "LEN([RequestHash]) = 64");
                    table.CheckConstraint("CK_AnswerRevisions_RevisionNumber", "[RevisionNumber] > 0");
                    table.CheckConstraint("CK_AnswerRevisions_TextLength", "[TextAnswer] IS NULL OR DATALENGTH([TextAnswer]) <= 40000");
                    table.CheckConstraint("CK_AnswerRevisions_WriteLeaseVersion", "[AcceptedWriteLeaseVersion] > 0");
                    table.ForeignKey(
                        name: "FK_AnswerRevisions_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.SupersedesAnswerRevisionId },
                        principalSchema: "assessment",
                        principalTable: "AnswerRevisions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerRevisions_ExamAttemptQuestions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.QuestionVersionId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttemptQuestions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "Id", "QuestionVersionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerRevisions_ExamAttempts_OrganizationId_ExamAttemptId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId },
                        principalSchema: "assessment",
                        principalTable: "ExamAttempts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerRevisions_QuestionOptions_OrganizationId_QuestionVersionId_SelectedOptionId",
                        columns: x => new { x.OrganizationId, x.QuestionVersionId, x.SelectedOptionId },
                        principalSchema: "assessment",
                        principalTable: "QuestionOptions",
                        principalColumns: new[] { "OrganizationId", "QuestionVersionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerRevisions_UserSessions_CreatedBySessionId",
                        column: x => x.CreatedBySessionId,
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_WriterSessionId",
                schema: "assessment",
                table: "ExamAttempts",
                column: "WriterSessionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_AnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[AnswerSetVersion] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_WriteLeaseState",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "([WriterSessionId] IS NULL AND [WriteLeaseVersion] = 0 AND [WriteLeaseAcquiredAtUtc] IS NULL) OR ([WriterSessionId] IS NOT NULL AND [WriteLeaseVersion] > 0 AND [WriteLeaseAcquiredAtUtc] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAttempts_WriteLeaseVersion",
                schema: "assessment",
                table: "ExamAttempts",
                sql: "[WriteLeaseVersion] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id", "CurrentAnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_CreatedBySessionId",
                schema: "assessment",
                table: "AnswerRevisions",
                column: "CreatedBySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_QuestionVersionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "QuestionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_SupersedesAnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "SupersedesAnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisions_OrganizationId_QuestionVersionId_SelectedOptionId",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "SelectedOptionId" });

            migrationBuilder.CreateIndex(
                name: "UX_AnswerRevisions_Organization_Attempt_ClientOperation",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AnswerRevisions_Organization_AttemptQuestion_Revision",
                schema: "assessment",
                table: "AnswerRevisions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "RevisionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttemptQuestions_AnswerRevisions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions",
                columns: new[] { "OrganizationId", "ExamAttemptId", "Id", "CurrentAnswerRevisionId" },
                principalSchema: "assessment",
                principalTable: "AnswerRevisions",
                principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAttempts_UserSessions_WriterSessionId",
                schema: "assessment",
                table: "ExamAttempts",
                column: "WriterSessionId",
                principalSchema: "identity",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttemptQuestions_AnswerRevisions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamAttempts_UserSessions_WriterSessionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropTable(
                name: "AnswerRevisions",
                schema: "assessment");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_QuestionOptions_Organization_QuestionVersion_Id",
                schema: "assessment",
                table: "QuestionOptions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttempts_Organization_Id",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_WriterSessionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_AnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_WriteLeaseState",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAttempts_WriteLeaseVersion",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttemptQuestions_OrganizationId_ExamAttemptId_Id_CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropColumn(
                name: "AnswerSetVersion",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "WriteLeaseAcquiredAtUtc",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "WriteLeaseVersion",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "WriterSessionId",
                schema: "assessment",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "CurrentAnswerRevisionId",
                schema: "assessment",
                table: "ExamAttemptQuestions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "assessment",
                table: "ExamAttemptQuestions");
        }
    }
}
