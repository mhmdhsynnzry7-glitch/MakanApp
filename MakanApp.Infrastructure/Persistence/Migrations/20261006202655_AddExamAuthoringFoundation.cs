using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAuthoringFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Exams",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    LatestPublishedVersionNumber = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                    table.UniqueConstraint("UQ_Exams_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Exams_CurrentVersionNumber", "[CurrentVersionNumber] > 0");
                    table.CheckConstraint("CK_Exams_PublicationState", "([Status] = 1 AND [LatestPublishedVersionNumber] IS NULL) OR ([Status] = 2 AND [LatestPublishedVersionNumber] IS NOT NULL)");
                    table.CheckConstraint("CK_Exams_PublishedVersion", "[LatestPublishedVersionNumber] IS NULL OR ([LatestPublishedVersionNumber] > 0 AND [LatestPublishedVersionNumber] <= [CurrentVersionNumber])");
                    table.CheckConstraint("CK_Exams_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Exams_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exams_Memberships_OrganizationId_CreatedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CreatedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamVersions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    AvailableFromUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AvailableUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    MaxScore = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    RandomizationPolicy = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamVersions", x => x.Id);
                    table.UniqueConstraint("UQ_ExamVersions_Organization_Exam_Id", x => new { x.OrganizationId, x.ExamId, x.Id });
                    table.UniqueConstraint("UQ_ExamVersions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_ExamVersions_Duration", "[DurationMinutes] > 0");
                    table.CheckConstraint("CK_ExamVersions_MaxAttempts", "[MaxAttempts] > 0");
                    table.CheckConstraint("CK_ExamVersions_MaxScore", "[MaxScore] > 0");
                    table.CheckConstraint("CK_ExamVersions_PublicationState", "([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] = 2 AND [PublishedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ExamVersions_Randomization", "[RandomizationPolicy] IN (1, 2)");
                    table.CheckConstraint("CK_ExamVersions_Status", "[Status] IN (1, 2)");
                    table.CheckConstraint("CK_ExamVersions_UpdatedAt", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.CheckConstraint("CK_ExamVersions_VersionNumber", "[VersionNumber] > 0");
                    table.CheckConstraint("CK_ExamVersions_Window", "[AvailableUntilUtc] > [AvailableFromUtc]");
                    table.ForeignKey(
                        name: "FK_ExamVersions_Exams_OrganizationId_ExamId",
                        columns: x => new { x.OrganizationId, x.ExamId },
                        principalSchema: "assessment",
                        principalTable: "Exams",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamVersions_Memberships_OrganizationId_CreatedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CreatedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionVersions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionVersions", x => x.Id);
                    table.UniqueConstraint("UQ_QuestionVersions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_QuestionVersions_Order", "[Order] > 0");
                    table.CheckConstraint("CK_QuestionVersions_Score", "[Score] > 0");
                    table.CheckConstraint("CK_QuestionVersions_Type", "[Type] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_QuestionVersions_ExamVersions_OrganizationId_ExamVersionId",
                        columns: x => new { x.OrganizationId, x.ExamVersionId },
                        principalSchema: "assessment",
                        principalTable: "ExamVersions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.CheckConstraint("CK_QuestionOptions_Order", "[Order] > 0");
                    table.ForeignKey(
                        name: "FK_QuestionOptions_QuestionVersions_OrganizationId_QuestionVersionId",
                        columns: x => new { x.OrganizationId, x.QuestionVersionId },
                        principalSchema: "assessment",
                        principalTable: "QuestionVersions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Organization_Class_Status",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "ClassId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_OrganizationId_CreatedByMembershipId",
                schema: "assessment",
                table: "Exams",
                columns: new[] { "OrganizationId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamVersions_OrganizationId_CreatedByMembershipId",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_Organization_Exam_ActiveDraft",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "ExamId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ExamVersions_Organization_Exam_VersionNumber",
                schema: "assessment",
                table: "ExamVersions",
                columns: new[] { "OrganizationId", "ExamId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QuestionOptions_Organization_Question_Order",
                schema: "assessment",
                table: "QuestionOptions",
                columns: new[] { "OrganizationId", "QuestionVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_QuestionVersions_Organization_ExamVersion_Order",
                schema: "assessment",
                table: "QuestionVersions",
                columns: new[] { "OrganizationId", "ExamVersionId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionOptions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "QuestionVersions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "ExamVersions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "Exams",
                schema: "assessment");
        }
    }
}
