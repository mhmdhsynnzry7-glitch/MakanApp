using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assessment");

            migrationBuilder.CreateTable(
                name: "Assignments",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.UniqueConstraint("UQ_Assignments_OrganizationId_ClassId_Id", x => new { x.OrganizationId, x.ClassId, x.Id });
                    table.CheckConstraint("CK_Assignments_CurrentVersionNumber", "[CurrentVersionNumber] > 0");
                    table.CheckConstraint("CK_Assignments_PublicationState", "([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] IN (2, 3, 4) AND [PublishedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Assignments_Status", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Assignments_UpdatedAt", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_Assignments_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Assignments_Memberships_OrganizationId_CreatedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CreatedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentVersions",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AllowLateSubmission = table.Column<bool>(type: "bit", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedByMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentVersions", x => x.Id);
                    table.UniqueConstraint("UQ_AssignmentVersions_Organization_Class_Assignment_Id", x => new { x.OrganizationId, x.ClassId, x.AssignmentId, x.Id });
                    table.CheckConstraint("CK_AssignmentVersions_DueAt", "[DueAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_AssignmentVersions_MaxAttempts", "[MaxAttempts] > 0");
                    table.CheckConstraint("CK_AssignmentVersions_PublishedDueAt", "[PublishedAtUtc] IS NULL OR [DueAtUtc] > [PublishedAtUtc]");
                    table.CheckConstraint("CK_AssignmentVersions_VersionNumber", "[VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_AssignmentVersions_Assignments_OrganizationId_ClassId_AssignmentId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.AssignmentId },
                        principalSchema: "assessment",
                        principalTable: "Assignments",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentVersions_Memberships_OrganizationId_CreatedByMembershipId",
                        columns: x => new { x.OrganizationId, x.CreatedByMembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRecipients",
                schema: "assessment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentRecipients_AssignmentVersions_OrganizationId_ClassId_AssignmentId_AssignmentVersionId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.AssignmentId, x.AssignmentVersionId },
                        principalSchema: "assessment",
                        principalTable: "AssignmentVersions",
                        principalColumns: new[] { "OrganizationId", "ClassId", "AssignmentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentRecipients_Enrollments_OrganizationId_ClassId_EnrollmentId",
                        columns: x => new { x.OrganizationId, x.ClassId, x.EnrollmentId },
                        principalSchema: "academic",
                        principalTable: "Enrollments",
                        principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRecipients_OrganizationId_ClassId_AssignmentId_AssignmentVersionId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "ClassId", "AssignmentId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRecipients_OrganizationId_ClassId_EnrollmentId",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "ClassId", "EnrollmentId" });

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentRecipients_Organization_Version_Enrollment",
                schema: "assessment",
                table: "AssignmentRecipients",
                columns: new[] { "OrganizationId", "AssignmentVersionId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Organization_Class_Status_UpdatedAt",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "OrganizationId", "ClassId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_OrganizationId_CreatedByMembershipId",
                schema: "assessment",
                table: "Assignments",
                columns: new[] { "OrganizationId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentVersions_OrganizationId_CreatedByMembershipId",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "CreatedByMembershipId" });

            migrationBuilder.CreateIndex(
                name: "UX_AssignmentVersions_Organization_Assignment_VersionNumber",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "AssignmentId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentRecipients",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "AssignmentVersions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "Assignments",
                schema: "assessment");
        }
    }
}
