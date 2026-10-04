using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectMessagingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.AddColumn<int>(
                name: "CommunicationAgeCategory",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DirectUserLowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DirectUserHighId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextMessageSequence = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.CheckConstraint("CK_Conversations_DirectPair", "[Type] <> 1 OR ([DirectUserLowId] IS NOT NULL AND [DirectUserHighId] IS NOT NULL AND [DirectUserLowId] <> [DirectUserHighId])");
                    table.CheckConstraint("CK_Conversations_NextMessageSequence", "[NextMessageSequence] > 0");
                    table.CheckConstraint("CK_Conversations_Scope", "[Scope] IN (1, 2)");
                    table.CheckConstraint("CK_Conversations_ScopeOrganization", "([Scope] = 1 AND [OrganizationId] IS NULL) OR ([Scope] = 2 AND [OrganizationId] IS NOT NULL)");
                    table.CheckConstraint("CK_Conversations_Status", "[Status] IN (1, 2)");
                    table.CheckConstraint("CK_Conversations_Type", "[Type] IN (1)");
                    table.ForeignKey(
                        name: "FK_Conversations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_DirectUserHighId",
                        column: x => x.DirectUserHighId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_DirectUserLowId",
                        column: x => x.DirectUserLowId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalCommunicationGrants",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LowerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HigherUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalCommunicationGrants", x => x.Id);
                    table.CheckConstraint("CK_PersonalCommunicationGrants_DistinctUsers", "[LowerUserId] <> [HigherUserId]");
                    table.CheckConstraint("CK_PersonalCommunicationGrants_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_PersonalCommunicationGrants_Users_HigherUserId",
                        column: x => x.HigherUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalCommunicationGrants_Users_LowerUserId",
                        column: x => x.LowerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    LeftAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                    table.UniqueConstraint("UQ_ConversationParticipants_Conversation_User", x => new { x.ConversationId, x.UserId });
                    table.CheckConstraint("CK_ConversationParticipants_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.CheckConstraint("CK_Messages_Kind", "[Kind] IN (1)");
                    table.CheckConstraint("CK_Messages_Sequence", "[Sequence] > 0");
                    table.CheckConstraint("CK_Messages_Text", "LEN(LTRIM(RTRIM([Text]))) > 0");
                    table.ForeignKey(
                        name: "FK_Messages_ConversationParticipants_ConversationId_SenderUserId",
                        columns: x => new { x.ConversationId, x.SenderUserId },
                        principalSchema: "messaging",
                        principalTable: "ConversationParticipants",
                        principalColumns: new[] { "ConversationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_CommunicationAgeCategory",
                schema: "identity",
                table: "Users",
                sql: "[CommunicationAgeCategory] IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_UserId_Status",
                schema: "messaging",
                table: "ConversationParticipants",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DirectUserHighId",
                schema: "messaging",
                table: "Conversations",
                column: "DirectUserHighId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_DirectUserLowId",
                schema: "messaging",
                table: "Conversations",
                column: "DirectUserLowId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId",
                schema: "messaging",
                table: "Conversations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "UX_Conversations_Direct_Scope_Organization_Pair",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "Scope", "OrganizationId", "DirectUserLowId", "DirectUserHighId" },
                unique: true,
                filter: "[Type] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Messages_Conversation_Sender_ClientMessageId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ConversationId", "SenderUserId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Messages_Conversation_Sequence",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalCommunicationGrants_HigherUserId",
                schema: "messaging",
                table: "PersonalCommunicationGrants",
                column: "HigherUserId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonalCommunicationGrants_UserPair",
                schema: "messaging",
                table: "PersonalCommunicationGrants",
                columns: new[] { "LowerUserId", "HigherUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "PersonalCommunicationGrants",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationParticipants",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_CommunicationAgeCategory",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CommunicationAgeCategory",
                schema: "identity",
                table: "Users");
        }
    }
}
