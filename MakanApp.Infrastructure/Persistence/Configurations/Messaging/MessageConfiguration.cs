using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages", "messaging", table =>
        {
            table.HasCheckConstraint("CK_Messages_Kind", "[Kind] IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint("CK_Messages_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_Messages_CurrentRevision", "[CurrentRevisionNumber] > 0");
            table.HasCheckConstraint(
                "CK_Messages_Content",
                "([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR " +
                "([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND " +
                "(([Kind] = 1 AND LEN(LTRIM(RTRIM([Text]))) > 0) OR " +
                "([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0))))");
        });
        builder.HasKey(message => message.Id);
        builder.HasAlternateKey(message => new { message.Id, message.ConversationId })
            .HasName("UQ_Messages_Id_Conversation");
        builder.Property(message => message.Text)
            .HasMaxLength(Message.StorageMaximumTextLength);
        builder.Property(message => message.SearchText)
            .HasMaxLength(Message.StorageMaximumTextLength);
        builder.Property(message => message.SentAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.EditedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.DeletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.RowVersion).IsRowVersion();

        builder.HasIndex(message => new { message.ConversationId, message.Sequence })
            .IsUnique()
            .HasDatabaseName("UX_Messages_Conversation_Sequence");
        builder.HasIndex(message => new
        {
            message.ConversationId,
            message.SenderUserId,
            message.ClientMessageId
        })
            .IsUnique()
            .HasDatabaseName("UX_Messages_Conversation_Sender_ClientMessageId");
        builder.HasIndex(message => message.ReplyToMessageId)
            .HasDatabaseName("IX_Messages_ReplyToMessageId");
        builder.HasIndex(message => message.ForwardedFromMessageId)
            .HasDatabaseName("IX_Messages_ForwardedFromMessageId");
        builder.HasIndex(message => new { message.ConversationId, message.SentAtUtc, message.Id })
            .HasDatabaseName("IX_Messages_Search_Access");

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConversationParticipant>()
            .WithMany()
            .HasForeignKey(message => new
            {
                message.SenderParticipantId,
                message.ConversationId,
                message.SenderUserId
            })
            .HasPrincipalKey(participant => new
            {
                participant.Id,
                participant.ConversationId,
                participant.UserId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(message => new { message.ReplyToMessageId, message.ConversationId })
            .HasPrincipalKey(message => new { message.Id, message.ConversationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(message => message.ForwardedFromMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(message => message.DeletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
