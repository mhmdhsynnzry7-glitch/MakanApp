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
            table.HasCheckConstraint("CK_ConversationParticipants_Status", "[Status] IN (1, 2, 3)"));
        builder.HasKey(participant => participant.Id);
        builder.HasAlternateKey(participant => new
        {
            participant.ConversationId,
            participant.UserId
        }).HasName("UQ_ConversationParticipants_Conversation_User");
        builder.Property(participant => participant.JoinedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(participant => participant.LeftAtUtc).HasColumnType("datetime2(7)");
        builder.Property(participant => participant.RowVersion).IsRowVersion();

        builder.HasIndex(participant => new { participant.UserId, participant.Status });

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
