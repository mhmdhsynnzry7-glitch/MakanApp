using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "guardian");

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedSubjectOrganizationPersonId",
                schema: "identity",
                table: "UserSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuardianRelations",
                schema: "guardian",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuardianUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearnerOrganizationPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuardianRelations", x => x.Id);
                    table.CheckConstraint("CK_GuardianRelations_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_GuardianRelations_OrganizationPersons_OrganizationId_LearnerOrganizationPersonId",
                        columns: x => new { x.OrganizationId, x.LearnerOrganizationPersonId },
                        principalSchema: "organization",
                        principalTable: "OrganizationPersons",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianRelations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuardianRelations_Users_GuardianUserId",
                        column: x => x.GuardianUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuardianRelations_GuardianUserId",
                schema: "guardian",
                table: "GuardianRelations",
                column: "GuardianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GuardianRelations_OrganizationId_LearnerOrganizationPersonId",
                schema: "guardian",
                table: "GuardianRelations",
                columns: new[] { "OrganizationId", "LearnerOrganizationPersonId" });

            migrationBuilder.CreateIndex(
                name: "UX_GuardianRelations_Active_Organization_Guardian_Learner",
                schema: "guardian",
                table: "GuardianRelations",
                columns: new[] { "OrganizationId", "GuardianUserId", "LearnerOrganizationPersonId" },
                unique: true,
                filter: "[Status] = 2 AND [EndedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuardianRelations",
                schema: "guardian");

            migrationBuilder.DropColumn(
                name: "SelectedSubjectOrganizationPersonId",
                schema: "identity",
                table: "UserSessions");
        }
    }
}
