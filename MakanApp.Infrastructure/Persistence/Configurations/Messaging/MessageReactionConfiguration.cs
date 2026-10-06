using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("MessageReactions", "messaging", table =>
        {
            table.HasCheckConstraint("CK_MessageReactions_Type", "[ReactionType] IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint(
                "CK_MessageReactions_Lifecycle",
                "[RemovedAtUtc] IS NULL OR [RemovedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(reaction => reaction.Id);
        builder.Property(reaction => reaction.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(reaction => reaction.RemovedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(reaction => reaction.RowVersion).IsRowVersion();
        builder.HasIndex(reaction => new { reaction.MessageId, reaction.UserId })
            .IsUnique()
            .HasFilter("[RemovedAtUtc] IS NULL")
            .HasDatabaseName("UX_MessageReactions_Active_Message_User");
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(reaction => reaction.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(reaction => reaction.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
