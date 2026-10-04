using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFileStorageFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "storage");

            migrationBuilder.CreateTable(
                name: "FileAssets",
                schema: "storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(127)", maxLength: 127, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    UnattachedExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAssets", x => x.Id);
                    table.CheckConstraint("CK_FileAssets_DeletedState", "([Status] = 4 AND [DeletedAtUtc] IS NOT NULL) OR [Status] <> 4");
                    table.CheckConstraint("CK_FileAssets_ReadyState", "([Status] = 2 AND [SizeBytes] > 0 AND [Sha256Hash] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL) OR [Status] <> 2");
                    table.CheckConstraint("CK_FileAssets_RejectedState", "([Status] = 3 AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 3");
                    table.CheckConstraint("CK_FileAssets_SizeBytes", "[SizeBytes] >= 0");
                    table.CheckConstraint("CK_FileAssets_Status", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_FileAssets_UnattachedExpiry", "[UnattachedExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_FileAssets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileAssets_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_OrganizationId",
                schema: "storage",
                table: "FileAssets",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_Status_CreatedAtUtc",
                schema: "storage",
                table: "FileAssets",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_Status_UnattachedExpiresAtUtc",
                schema: "storage",
                table: "FileAssets",
                columns: new[] { "Status", "UnattachedExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_Uploader_Organization_Status",
                schema: "storage",
                table: "FileAssets",
                columns: new[] { "UploadedByUserId", "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_FileAssets_StorageKey",
                schema: "storage",
                table: "FileAssets",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileAssets",
                schema: "storage");
        }
    }
}
