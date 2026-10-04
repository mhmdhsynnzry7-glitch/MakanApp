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
            table.HasCheckConstraint("CK_Messages_Kind", "[Kind] IN (1)");
            table.HasCheckConstraint("CK_Messages_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_Messages_Text", "LEN(LTRIM(RTRIM([Text]))) > 0");
        });
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Text)
            .HasMaxLength(Message.StorageMaximumTextLength)
            .IsRequired();
        builder.Property(message => message.SentAtUtc).HasColumnType("datetime2(7)");

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

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConversationParticipant>()
            .WithMany()
            .HasForeignKey(message => new { message.ConversationId, message.SenderUserId })
            .HasPrincipalKey(participant => new { participant.ConversationId, participant.UserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
