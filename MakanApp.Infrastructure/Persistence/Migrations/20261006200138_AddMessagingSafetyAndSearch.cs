using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingSafetyAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                schema: "messaging",
                table: "Messages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [messaging].[Messages]
                SET [SearchText] = LTRIM(RTRIM(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                        [Text], N'ي', N'ی'), N'ى', N'ی'), N'ك', N'ک'), NCHAR(8204), N' '),
                        CHAR(13), N' '), CHAR(10), N' '), CHAR(9), N' '), N'  ', N' '), N'  ', N' ')))
                WHERE [DeletedAtUtc] IS NULL AND [Text] IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "AbuseReports",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedMessageKind = table.Column<int>(type: "int", nullable: false),
                    ReportedContentSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReportedMessageVersion = table.Column<byte[]>(type: "binary(8)", nullable: false),
                    RequestPayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbuseReports", x => x.Id);
                    table.CheckConstraint("CK_AbuseReports_MessageKind", "[ReportedMessageKind] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_AbuseReports_Reason", "[Reason] IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_AbuseReports_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_AbuseReports_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbuseReports_Messages_MessageId_ConversationId",
                        columns: x => new { x.MessageId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbuseReports_Users_ReportedUserId",
                        column: x => x.ReportedUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbuseReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserBlocks",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlocks", x => x.Id);
                    table.CheckConstraint("CK_UserBlocks_DistinctUsers", "[BlockerUserId] <> [BlockedUserId]");
                    table.CheckConstraint("CK_UserBlocks_Lifecycle", "([Status] = 1 AND [EndedAtUtc] IS NULL) OR ([Status] = 2 AND [EndedAtUtc] IS NOT NULL AND [EndedAtUtc] >= [CreatedAtUtc])");
                    table.CheckConstraint("CK_UserBlocks_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_UserBlocks_Users_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserBlocks_Users_BlockerUserId",
                        column: x => x.BlockerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Search_Access",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AbuseReports_ConversationId",
                schema: "messaging",
                table: "AbuseReports",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AbuseReports_MessageId_ConversationId",
                schema: "messaging",
                table: "AbuseReports",
                columns: new[] { "MessageId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AbuseReports_ReportedUserId",
                schema: "messaging",
                table: "AbuseReports",
                column: "ReportedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AbuseReports_Status_CreatedAtUtc",
                schema: "messaging",
                table: "AbuseReports",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_AbuseReports_Reporter_ClientReportId",
                schema: "messaging",
                table: "AbuseReports",
                columns: new[] { "ReporterUserId", "ClientReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockedUserId",
                schema: "messaging",
                table: "UserBlocks",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_Blocker_CreatedAtUtc",
                schema: "messaging",
                table: "UserBlocks",
                columns: new[] { "BlockerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_UserBlocks_Active_DirectionalPair",
                schema: "messaging",
                table: "UserBlocks",
                columns: new[] { "BlockerUserId", "BlockedUserId" },
                unique: true,
                filter: "[EndedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbuseReports",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "UserBlocks",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "IX_Messages_Search_Access",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SearchText",
                schema: "messaging",
                table: "Messages");
        }
    }
}
