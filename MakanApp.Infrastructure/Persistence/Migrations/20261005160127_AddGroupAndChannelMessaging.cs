using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupAndChannelMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_ConversationParticipants_ConversationId_SenderUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ConversationParticipants_Conversation_User",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.RenameColumn(
                name: "LeftAtUtc",
                schema: "messaging",
                table: "ConversationParticipants",
                newName: "EndedAtUtc");

            migrationBuilder.AddColumn<Guid>(
                name: "SenderParticipantId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                schema: "messaging",
                table: "Conversations",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientOperationId",
                schema: "messaging",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "messaging",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CreationPayloadHash",
                schema: "messaging",
                table: "Conversations",
                type: "binary(32)",
                fixedLength: true,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "messaging",
                table: "Conversations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManagementPolicy",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "messaging",
                table: "Conversations",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EndedByUserId",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [messaging].[Messages]
                SET [SenderParticipantId] = [participant].[Id]
                FROM [messaging].[Messages] AS [message]
                INNER JOIN [messaging].[ConversationParticipants] AS [participant]
                    ON [participant].[ConversationId] = [message].[ConversationId]
                    AND [participant].[UserId] = [message].[SenderUserId];

                UPDATE [messaging].[ConversationParticipants]
                SET [Role] = 3,
                    [EndedByUserId] = CASE WHEN [Status] IN (2, 3) THEN [UserId] ELSE NULL END;

                UPDATE [messaging].[Conversations]
                SET [ManagementPolicy] = 0;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SenderParticipantId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ManagementPolicy",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ConversationParticipants_Id_Conversation_User",
                schema: "messaging",
                table: "ConversationParticipants",
                columns: new[] { "Id", "ConversationId", "UserId" });

            migrationBuilder.CreateTable(
                name: "ConversationOwnershipTransfers",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationOwnershipTransfers", x => x.Id);
                    table.CheckConstraint("CK_ConversationOwnershipTransfers_Status", "[Status] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_ConversationOwnershipTransfers_ConversationParticipants_FromParticipantId_ConversationId_FromUserId",
                        columns: x => new { x.FromParticipantId, x.ConversationId, x.FromUserId },
                        principalSchema: "messaging",
                        principalTable: "ConversationParticipants",
                        principalColumns: new[] { "Id", "ConversationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationOwnershipTransfers_ConversationParticipants_ToParticipantId_ConversationId_ToUserId",
                        columns: x => new { x.ToParticipantId, x.ConversationId, x.ToUserId },
                        principalSchema: "messaging",
                        principalTable: "ConversationParticipants",
                        principalColumns: new[] { "Id", "ConversationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationOwnershipTransfers_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationOwnershipTransfers_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationOwnershipTransfers_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderParticipantId_ConversationId_SenderUserId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "SenderParticipantId", "ConversationId", "SenderUserId" });

            migrationBuilder.CreateIndex(
                name: "UX_Conversations_Creator_ClientOperationId",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "CreatedByUserId", "ClientOperationId" },
                unique: true,
                filter: "[ManagementPolicy] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_ManagedShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] = 1 AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] IN (2, 3) AND [ManagementPolicy] IN (1, 2) AND LEN(LTRIM(RTRIM([Title]))) > 0 AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_ManagementPolicy",
                schema: "messaging",
                table: "Conversations",
                sql: "[ManagementPolicy] IN (0, 1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_SystemManagedAcademic",
                schema: "messaging",
                table: "Conversations",
                sql: "([ManagementPolicy] <> 2) OR ([Scope] = 2 AND [CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationPayloadHash] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations",
                sql: "[Type] IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_UserManagedCreation",
                schema: "messaging",
                table: "Conversations",
                sql: "([ManagementPolicy] <> 1) OR ([CreatedByUserId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_EndedByUserId",
                schema: "messaging",
                table: "ConversationParticipants",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationParticipants_Active_Conversation_User",
                schema: "messaging",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "UserId" },
                unique: true,
                filter: "[Status] = 1 AND [EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationParticipants_ActiveOwner",
                schema: "messaging",
                table: "ConversationParticipants",
                column: "ConversationId",
                unique: true,
                filter: "[Role] = 1 AND [Status] = 1 AND [EndedAtUtc] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_Lifecycle",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "([Status] = 1 AND [EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL) OR ([Status] IN (2, 3) AND [EndedAtUtc] IS NOT NULL AND [EndedByUserId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_Role",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[Role] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationOwnershipTransfers_FromParticipantId_ConversationId_FromUserId",
                schema: "messaging",
                table: "ConversationOwnershipTransfers",
                columns: new[] { "FromParticipantId", "ConversationId", "FromUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationOwnershipTransfers_FromUserId",
                schema: "messaging",
                table: "ConversationOwnershipTransfers",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationOwnershipTransfers_ToParticipantId_ConversationId_ToUserId",
                schema: "messaging",
                table: "ConversationOwnershipTransfers",
                columns: new[] { "ToParticipantId", "ConversationId", "ToUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationOwnershipTransfers_ToUserId",
                schema: "messaging",
                table: "ConversationOwnershipTransfers",
                column: "ToUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationOwnershipTransfers_Pending",
                schema: "messaging",
                table: "ConversationOwnershipTransfers",
                column: "ConversationId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationParticipants_Users_EndedByUserId",
                schema: "messaging",
                table: "ConversationParticipants",
                column: "EndedByUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_CreatedByUserId",
                schema: "messaging",
                table: "Conversations",
                column: "CreatedByUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ConversationParticipants_SenderParticipantId_ConversationId_SenderUserId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "SenderParticipantId", "ConversationId", "SenderUserId" },
                principalSchema: "messaging",
                principalTable: "ConversationParticipants",
                principalColumns: new[] { "Id", "ConversationId", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationParticipants_Users_EndedByUserId",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_CreatedByUserId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_ConversationParticipants_SenderParticipantId_ConversationId_SenderUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "ConversationOwnershipTransfers",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderParticipantId_ConversationId_SenderUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "UX_Conversations_Creator_ClientOperationId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_ManagedShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_ManagementPolicy",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_SystemManagedAcademic",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_UserManagedCreation",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_ConversationParticipants_Id_Conversation_User",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropIndex(
                name: "IX_ConversationParticipants_EndedByUserId",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropIndex(
                name: "UX_ConversationParticipants_Active_Conversation_User",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropIndex(
                name: "UX_ConversationParticipants_ActiveOwner",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_Lifecycle",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_Role",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "SenderParticipantId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ClientOperationId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CreationPayloadHash",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ManagementPolicy",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EndedByUserId",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.RenameColumn(
                name: "EndedAtUtc",
                schema: "messaging",
                table: "ConversationParticipants",
                newName: "LeftAtUtc");

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_ConversationParticipants_Conversation_User",
                schema: "messaging",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "UserId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations",
                sql: "[Type] IN (1)");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ConversationParticipants_ConversationId_SenderUserId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ConversationId", "SenderUserId" },
                principalSchema: "messaging",
                principalTable: "ConversationParticipants",
                principalColumns: new[] { "ConversationId", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
