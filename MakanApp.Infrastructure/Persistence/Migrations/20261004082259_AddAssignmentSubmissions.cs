using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RetainedAtUtc",
                schema: "storage",
                table: "FileAssets",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "AssignmentId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_AssignmentRecipients_SubmissionScope_Id",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "Id" });

            migrationBuilder.CreateTable(
                name: "SubmissionAttempts",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentRecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    LastSavedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsLate = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAttempts", x => x.Id);
                    table.UniqueConstraint("UQ_SubmissionAttempts_Organization_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_SubmissionAttempts_AttemptNumber", "[AttemptNumber] > 0");
                    table.CheckConstraint("CK_SubmissionAttempts_State", "([Status] = 1 AND [SubmittedAtUtc] IS NULL AND [IsLate] = 0) OR ([Status] = 2 AND [SubmittedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_SubmissionAttempts_Status", "[Status] IN (1, 2)");
                    table.CheckConstraint("CK_SubmissionAttempts_Timestamps", "[LastSavedAtUtc] IS NULL OR [LastSavedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_SubmissionAttempts_AssignmentRecipients_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId",
                        columns: x => new { x.OrganizationId, x.AssignmentId, x.AssignmentVersionId, x.EnrollmentId, x.AssignmentRecipientId },
                        principalSchema: "assessment",
                        principalTable: "AssignmentRecipients",
                        principalColumns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionAttempts_AssignmentVersions_OrganizationId_AssignmentId_AssignmentVersionId",
                        columns: x => new { x.OrganizationId, x.AssignmentId, x.AssignmentVersionId },
                        principalSchema: "assessment",
                        principalTable: "AssignmentVersions",
                        principalColumns: new[] { "OrganizationId", "AssignmentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionAttachments",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubmissionAttachments_SubmissionAttempts_OrganizationId_SubmissionAttemptId",
                        columns: x => new { x.OrganizationId, x.SubmissionAttemptId },
                        principalSchema: "assessment",
                        principalTable: "SubmissionAttempts",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FileAssets_RetentionState",
                schema: "storage",
                table: "FileAssets",
                sql: "[RetainedAtUtc] IS NULL OR [Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttachments_FileAssetId",
                schema: "assessment",
                table: "SubmissionAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttachments_Attempt_FileAsset",
                schema: "assessment",
                table: "SubmissionAttachments",
                columns: new[] { "OrganizationId", "SubmissionAttemptId", "FileAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAttempts_OrganizationId_AssignmentId_AssignmentVersionId_EnrollmentId_AssignmentRecipientId",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentId", "AssignmentVersionId", "EnrollmentId", "AssignmentRecipientId" });

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttempts_OneDraft",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentRecipientId", "AssignmentVersionId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_SubmissionAttempts_Recipient_Version_Number",
                schema: "assessment",
                table: "SubmissionAttempts",
                columns: new[] { "OrganizationId", "AssignmentRecipientId", "AssignmentVersionId", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionAttachments",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "SubmissionAttempts",
                schema: "assessment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FileAssets_RetentionState",
                schema: "storage",
                table: "FileAssets");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentVersions_Organization_Assignment_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_AssignmentRecipients_SubmissionScope_Id",
                schema: "assessment",
                table: "AssignmentRecipients");

            migrationBuilder.DropColumn(
                name: "RetainedAtUtc",
                schema: "storage",
                table: "FileAssets");
        }
    }
}
