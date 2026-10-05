using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationParticipantConfiguration :
    IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("ConversationParticipants", "messaging", table =>
        {
            table.HasCheckConstraint("CK_ConversationParticipants_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ConversationParticipants_Role", "[Role] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_ConversationParticipants_Lifecycle",
                "([Status] = 1 AND [EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL) OR ([Status] IN (2, 3) AND [EndedAtUtc] IS NOT NULL AND [EndedByUserId] IS NOT NULL)");
        });
        builder.HasKey(participant => participant.Id);
        builder.HasAlternateKey(participant => new
        {
            participant.Id,
            participant.ConversationId,
            participant.UserId
        }).HasName("UQ_ConversationParticipants_Id_Conversation_User");
        builder.Property(participant => participant.JoinedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(participant => participant.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(participant => participant.RowVersion).IsRowVersion();

        builder.HasIndex(participant => new { participant.UserId, participant.Status });
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_ConversationParticipants_Active_Conversation_User");
        builder.HasIndex(participant => participant.ConversationId)
            .IsUnique()
            .HasFilter("[Role] = 1 AND [Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_ConversationParticipants_ActiveOwner");

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(participant => participant.EndedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
