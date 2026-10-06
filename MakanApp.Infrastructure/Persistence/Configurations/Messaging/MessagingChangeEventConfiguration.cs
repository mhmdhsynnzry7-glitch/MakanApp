using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessagingChangeEventConfiguration : IEntityTypeConfiguration<MessagingChangeEvent>
{
    public void Configure(EntityTypeBuilder<MessagingChangeEvent> builder)
    {
        builder.ToTable("ChangeEvents", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ChangeEvents_Sequence", "[ChangeSequence] > 0");
            table.HasCheckConstraint("CK_ChangeEvents_Type", "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9)");
            table.HasCheckConstraint("CK_ChangeEvents_PayloadVersion", "[PayloadVersion] = 1");
        });
        builder.HasKey(change => change.Id);
        builder.Property(change => change.ResourceVersion)
            .HasMaxLength(MessagingChangeEvent.ResourceVersionMaximumLength);
        builder.Property(change => change.OccurredAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(change => new { change.ConversationId, change.ChangeSequence })
            .IsUnique()
            .HasDatabaseName("UX_ChangeEvents_Conversation_Sequence");
        builder.HasIndex(change => new { change.ConversationId, change.AudienceUserId, change.ChangeSequence })
            .HasDatabaseName("IX_ChangeEvents_Conversation_Audience_Sequence");
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(change => change.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(change => change.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(change => change.AudienceUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
