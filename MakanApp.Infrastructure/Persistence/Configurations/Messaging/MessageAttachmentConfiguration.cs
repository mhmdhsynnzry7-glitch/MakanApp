using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("MessageAttachments", "messaging", table =>
            table.HasCheckConstraint("CK_MessageAttachments_Kind", "[Kind] IN (2, 3, 4, 5)"));
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(attachment => new { attachment.MessageId, attachment.FileAssetId })
            .IsUnique()
            .HasDatabaseName("UX_MessageAttachments_Message_File");
        builder.HasIndex(attachment => attachment.FileAssetId)
            .HasDatabaseName("IX_MessageAttachments_FileAssetId");
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileAsset>()
            .WithMany()
            .HasForeignKey(attachment => attachment.FileAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
