using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organization");

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedMembershipId",
                schema: "identity",
                table: "UserSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedRole",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.CheckConstraint("CK_Organizations_Status", "[Status] IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => x.Id);
                    table.CheckConstraint("CK_Memberships_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Memberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Memberships_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.Id);
                    table.CheckConstraint("CK_RoleAssignments_Role", "[Role] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_RoleAssignments_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AcceptedMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedRoleAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.CheckConstraint("CK_Invitations_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_Invitations_Role", "[Role] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Invitations_Status", "[Status] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Invitations_Memberships_AcceptedMembershipId",
                        column: x => x.AcceptedMembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_RoleAssignments_AcceptedRoleAssignmentId",
                        column: x => x.AcceptedRoleAssignmentId,
                        principalSchema: "organization",
                        principalTable: "RoleAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_DestinationUserId",
                        column: x => x.DestinationUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedMembershipId",
                schema: "organization",
                table: "Invitations",
                column: "AcceptedMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedRoleAssignmentId",
                schema: "organization",
                table: "Invitations",
                column: "AcceptedRoleAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByUserId",
                schema: "organization",
                table: "Invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OrganizationId",
                schema: "organization",
                table: "Invitations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_Pending_Destination_Organization_Role",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "DestinationUserId", "OrganizationId", "Role" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_OrganizationId",
                schema: "organization",
                table: "Memberships",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "UX_Memberships_Active_User_Organization",
                schema: "organization",
                table: "Memberships",
                columns: new[] { "UserId", "OrganizationId" },
                unique: true,
                filter: "[Status] = 1 AND [EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RoleAssignments_Active_Membership_Role",
                schema: "organization",
                table: "RoleAssignments",
                columns: new[] { "MembershipId", "Role" },
                unique: true,
                filter: "[Status] = 1 AND [EndedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "RoleAssignments",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "Memberships",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "organization");

            migrationBuilder.DropColumn(
                name: "SelectedMembershipId",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "SelectedRole",
                schema: "identity",
                table: "UserSessions");
        }
    }
}
