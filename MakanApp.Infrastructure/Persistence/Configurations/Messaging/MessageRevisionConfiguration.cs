using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessageRevisionConfiguration : IEntityTypeConfiguration<MessageRevision>
{
    public void Configure(EntityTypeBuilder<MessageRevision> builder)
    {
        builder.ToTable("MessageRevisions", "messaging", table =>
            table.HasCheckConstraint("CK_MessageRevisions_Number", "[RevisionNumber] > 0"));
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.Text).HasMaxLength(Message.StorageMaximumTextLength);
        builder.Property(revision => revision.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(revision => new { revision.MessageId, revision.RevisionNumber })
            .IsUnique()
            .HasDatabaseName("UX_MessageRevisions_Message_Number");
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(revision => revision.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(revision => revision.AuthoredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
