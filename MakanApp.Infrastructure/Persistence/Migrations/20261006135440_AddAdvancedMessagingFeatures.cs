using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedMessagingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Text",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "messaging",
                table: "Messages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AddColumn<int>(
                name: "CurrentRevisionNumber",
                schema: "messaging",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "messaging",
                table: "Messages",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAtUtc",
                schema: "messaging",
                table: "Messages",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ForwardedFromMessageId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "messaging",
                table: "Messages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Messages_Id_Conversation",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "Id", "ConversationId" });

            migrationBuilder.CreateTable(
                name: "ConversationPins",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UnpinnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnpinnedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPins", x => x.Id);
                    table.CheckConstraint("CK_ConversationPins_Lifecycle", "([UnpinnedAtUtc] IS NULL AND [UnpinnedByUserId] IS NULL) OR ([UnpinnedAtUtc] IS NOT NULL AND [UnpinnedByUserId] IS NOT NULL AND [UnpinnedAtUtc] >= [PinnedAtUtc])");
                    table.ForeignKey(
                        name: "FK_ConversationPins_Messages_MessageId_ConversationId",
                        columns: x => new { x.MessageId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationPins_Users_PinnedByUserId",
                        column: x => x.PinnedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationPins_Users_UnpinnedByUserId",
                        column: x => x.UnpinnedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageAttachments",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageAttachments", x => x.Id);
                    table.CheckConstraint("CK_MessageAttachments_Kind", "[Kind] IN (2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_MessageAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageAttachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageMentions",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageMentions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageMentions_Users_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageReactions",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReactionType = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReactions", x => x.Id);
                    table.CheckConstraint("CK_MessageReactions_Lifecycle", "[RemovedAtUtc] IS NULL OR [RemovedAtUtc] >= [CreatedAtUtc]");
                    table.CheckConstraint("CK_MessageReactions_Type", "[ReactionType] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_MessageReactions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageReactions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageRevisions",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AuthoredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRevisions", x => x.Id);
                    table.CheckConstraint("CK_MessageRevisions_Number", "[RevisionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_MessageRevisions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageRevisions_Users_AuthoredByUserId",
                        column: x => x.AuthoredByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE [messaging].[Messages]
                SET [CurrentRevisionNumber] = 1
                WHERE [CurrentRevisionNumber] IS NULL;

                INSERT INTO [messaging].[MessageRevisions]
                    ([Id], [MessageId], [RevisionNumber], [Text], [AuthoredByUserId], [CreatedAtUtc])
                SELECT NEWID(), [Id], 1, [Text], [SenderUserId], [SentAtUtc]
                FROM [messaging].[Messages];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentRevisionNumber",
                schema: "messaging",
                table: "Messages",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DeletedByUserId",
                schema: "messaging",
                table: "Messages",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ForwardedFromMessageId",
                schema: "messaging",
                table: "Messages",
                column: "ForwardedFromMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                schema: "messaging",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId_ConversationId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ReplyToMessageId", "ConversationId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages",
                sql: "([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR ([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND (([Kind] = 1 AND LEN(LTRIM(RTRIM([Text]))) > 0) OR ([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_CurrentRevision",
                schema: "messaging",
                table: "Messages",
                sql: "[CurrentRevisionNumber] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages",
                sql: "[Kind] IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPins_MessageId_ConversationId",
                schema: "messaging",
                table: "ConversationPins",
                columns: new[] { "MessageId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPins_PinnedByUserId",
                schema: "messaging",
                table: "ConversationPins",
                column: "PinnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPins_UnpinnedByUserId",
                schema: "messaging",
                table: "ConversationPins",
                column: "UnpinnedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ConversationPins_Active_Conversation_Message",
                schema: "messaging",
                table: "ConversationPins",
                columns: new[] { "ConversationId", "MessageId" },
                unique: true,
                filter: "[UnpinnedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageAttachments_FileAssetId",
                schema: "messaging",
                table: "MessageAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "UX_MessageAttachments_Message_File",
                schema: "messaging",
                table: "MessageAttachments",
                columns: new[] { "MessageId", "FileAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageMentions_MentionedUserId",
                schema: "messaging",
                table: "MessageMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "UX_MessageMentions_Message_User",
                schema: "messaging",
                table: "MessageMentions",
                columns: new[] { "MessageId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_UserId",
                schema: "messaging",
                table: "MessageReactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_MessageReactions_Active_Message_User",
                schema: "messaging",
                table: "MessageReactions",
                columns: new[] { "MessageId", "UserId" },
                unique: true,
                filter: "[RemovedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRevisions_AuthoredByUserId",
                schema: "messaging",
                table: "MessageRevisions",
                column: "AuthoredByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_MessageRevisions_Message_Number",
                schema: "messaging",
                table: "MessageRevisions",
                columns: new[] { "MessageId", "RevisionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ForwardedFromMessageId",
                schema: "messaging",
                table: "Messages",
                column: "ForwardedFromMessageId",
                principalSchema: "messaging",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId_ConversationId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ReplyToMessageId", "ConversationId" },
                principalSchema: "messaging",
                principalTable: "Messages",
                principalColumns: new[] { "Id", "ConversationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                schema: "messaging",
                table: "Messages",
                column: "DeletedByUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ForwardedFromMessageId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId_ConversationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_DeletedByUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "ConversationPins",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "MessageAttachments",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "MessageMentions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "MessageReactions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "MessageRevisions",
                schema: "messaging");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Messages_Id_Conversation",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_DeletedByUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ForwardedFromMessageId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId_ConversationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_CurrentRevision",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CurrentRevisionNumber",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "EditedAtUtc",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ForwardedFromMessageId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                schema: "messaging",
                table: "Messages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages",
                sql: "[Kind] IN (1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Text",
                schema: "messaging",
                table: "Messages",
                sql: "LEN(LTRIM(RTRIM([Text]))) > 0");
        }
    }
}
