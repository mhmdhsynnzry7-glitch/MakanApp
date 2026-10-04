using MakanApp.Domain.Identity;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Storage;

public sealed class FileAssetConfiguration : IEntityTypeConfiguration<FileAsset>
{
    public void Configure(EntityTypeBuilder<FileAsset> builder)
    {
        builder.ToTable("FileAssets", "storage", table =>
        {
            table.HasCheckConstraint("CK_FileAssets_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_FileAssets_SizeBytes", "[SizeBytes] >= 0");
            table.HasCheckConstraint(
                "CK_FileAssets_ReadyState",
                "([Status] = 2 AND [SizeBytes] > 0 AND [Sha256Hash] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL) OR [Status] <> 2");
            table.HasCheckConstraint(
                "CK_FileAssets_RejectedState",
                "([Status] = 3 AND [RejectedAtUtc] IS NOT NULL) OR [Status] <> 3");
            table.HasCheckConstraint(
                "CK_FileAssets_DeletedState",
                "([Status] = 4 AND [DeletedAtUtc] IS NOT NULL) OR [Status] <> 4");
            table.HasCheckConstraint(
                "CK_FileAssets_UnattachedExpiry",
                "[UnattachedExpiresAtUtc] > [CreatedAtUtc]");
        });

        builder.HasKey(fileAsset => fileAsset.Id);
        builder.Property(fileAsset => fileAsset.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(fileAsset => fileAsset.StorageKey).HasMaxLength(180).IsRequired();
        builder.Property(fileAsset => fileAsset.ContentType).HasMaxLength(127).IsRequired();
        builder.Property(fileAsset => fileAsset.Sha256Hash).HasMaxLength(64).IsUnicode(false);
        builder.Property(fileAsset => fileAsset.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(fileAsset => fileAsset.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(fileAsset => fileAsset.RejectedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(fileAsset => fileAsset.DeletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(fileAsset => fileAsset.UnattachedExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(fileAsset => fileAsset.RowVersion).IsRowVersion();

        builder.HasIndex(fileAsset => fileAsset.StorageKey)
            .IsUnique()
            .HasDatabaseName("UX_FileAssets_StorageKey");
        builder.HasIndex(fileAsset => new { fileAsset.Status, fileAsset.CreatedAtUtc })
            .HasDatabaseName("IX_FileAssets_Status_CreatedAtUtc");
        builder.HasIndex(fileAsset => new { fileAsset.Status, fileAsset.UnattachedExpiresAtUtc })
            .HasDatabaseName("IX_FileAssets_Status_UnattachedExpiresAtUtc");
        builder.HasIndex(fileAsset => new
        {
            fileAsset.UploadedByUserId,
            fileAsset.OrganizationId,
            fileAsset.Status
        }).HasDatabaseName("IX_FileAssets_Uploader_Organization_Status");

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(fileAsset => fileAsset.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(fileAsset => fileAsset.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
