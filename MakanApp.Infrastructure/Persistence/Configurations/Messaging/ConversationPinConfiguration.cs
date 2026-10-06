using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationPinConfiguration : IEntityTypeConfiguration<ConversationPin>
{
    public void Configure(EntityTypeBuilder<ConversationPin> builder)
    {
        builder.ToTable("ConversationPins", "messaging", table =>
            table.HasCheckConstraint(
                "CK_ConversationPins_Lifecycle",
                "([UnpinnedAtUtc] IS NULL AND [UnpinnedByUserId] IS NULL) OR " +
                "([UnpinnedAtUtc] IS NOT NULL AND [UnpinnedByUserId] IS NOT NULL AND [UnpinnedAtUtc] >= [PinnedAtUtc])"));
        builder.HasKey(pin => pin.Id);
        builder.Property(pin => pin.PinnedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(pin => pin.UnpinnedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(pin => pin.RowVersion).IsRowVersion();
        builder.HasIndex(pin => new { pin.ConversationId, pin.MessageId })
            .IsUnique()
            .HasFilter("[UnpinnedAtUtc] IS NULL")
            .HasDatabaseName("UX_ConversationPins_Active_Conversation_Message");
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(pin => new { pin.MessageId, pin.ConversationId })
            .HasPrincipalKey(message => new { message.Id, message.ConversationId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(pin => pin.PinnedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(pin => pin.UnpinnedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
