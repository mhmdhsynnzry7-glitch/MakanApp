using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingRealtimeAndSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NextChangeSequence",
                schema: "messaging",
                table: "Conversations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CursorUpdatedAtUtc",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastDeliveredMessageSequence",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LastReadMessageSequence",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ChangeEvents",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeSequence = table.Column<long>(type: "bigint", nullable: false),
                    ChangeType = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AudienceUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeEvents", x => x.Id);
                    table.CheckConstraint("CK_ChangeEvents_PayloadVersion", "[PayloadVersion] = 1");
                    table.CheckConstraint("CK_ChangeEvents_Sequence", "[ChangeSequence] > 0");
                    table.CheckConstraint("CK_ChangeEvents_Type", "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9)");
                    table.ForeignKey(
                        name: "FK_ChangeEvents_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChangeEvents_Users_AudienceUserId",
                        column: x => x.AudienceUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RealtimeOutbox",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealtimeOutbox", x => x.Id);
                    table.CheckConstraint("CK_RealtimeOutbox_AttemptCount", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_RealtimeOutbox_Lifecycle", "[DispatchedAtUtc] IS NULL OR ([ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_RealtimeOutbox_ChangeEvents_ChangeEventId",
                        column: x => x.ChangeEventId,
                        principalSchema: "messaging",
                        principalTable: "ChangeEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE [messaging].[Conversations]
                SET [NextChangeSequence] = 1
                WHERE [NextChangeSequence] IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "NextChangeSequence",
                schema: "messaging",
                table: "Conversations",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_NextChangeSequence",
                schema: "messaging",
                table: "Conversations",
                sql: "[NextChangeSequence] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_Cursors",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[LastDeliveredMessageSequence] >= 0 AND [LastReadMessageSequence] >= 0 AND [LastReadMessageSequence] <= [LastDeliveredMessageSequence]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_CursorTimestamp",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "([LastDeliveredMessageSequence] = 0 AND [LastReadMessageSequence] = 0 AND [CursorUpdatedAtUtc] IS NULL) OR ([LastDeliveredMessageSequence] > 0 AND [CursorUpdatedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeEvents_ActorUserId",
                schema: "messaging",
                table: "ChangeEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeEvents_AudienceUserId",
                schema: "messaging",
                table: "ChangeEvents",
                column: "AudienceUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeEvents_Conversation_Audience_Sequence",
                schema: "messaging",
                table: "ChangeEvents",
                columns: new[] { "ConversationId", "AudienceUserId", "ChangeSequence" });

            migrationBuilder.CreateIndex(
                name: "UX_ChangeEvents_Conversation_Sequence",
                schema: "messaging",
                table: "ChangeEvents",
                columns: new[] { "ConversationId", "ChangeSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RealtimeOutbox_Pending",
                schema: "messaging",
                table: "RealtimeOutbox",
                columns: new[] { "DispatchedAtUtc", "NextAttemptAtUtc", "ClaimedUntilUtc", "OccurredAtUtc" },
                filter: "[DispatchedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RealtimeOutbox_ChangeEvent",
                schema: "messaging",
                table: "RealtimeOutbox",
                column: "ChangeEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RealtimeOutbox",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ChangeEvents",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_NextChangeSequence",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_Cursors",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_CursorTimestamp",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "NextChangeSequence",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CursorUpdatedAtUtc",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "LastDeliveredMessageSequence",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "LastReadMessageSequence",
                schema: "messaging",
                table: "ConversationParticipants");
        }
    }
}
