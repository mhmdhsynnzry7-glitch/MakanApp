using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationOwnershipTransferConfiguration :
    IEntityTypeConfiguration<ConversationOwnershipTransfer>
{
    public void Configure(EntityTypeBuilder<ConversationOwnershipTransfer> builder)
    {
        builder.ToTable("ConversationOwnershipTransfers", "messaging", table =>
            table.HasCheckConstraint(
                "CK_ConversationOwnershipTransfers_Status",
                "[Status] IN (1, 2, 3, 4, 5)"));
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(transfer => transfer.AcceptedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(transfer => transfer.DeclinedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(transfer => transfer.RowVersion).IsRowVersion();

        builder.HasIndex(transfer => transfer.ConversationId)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_ConversationOwnershipTransfers_Pending");

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(transfer => transfer.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConversationParticipant>()
            .WithMany()
            .HasForeignKey(transfer => new
            {
                transfer.FromParticipantId,
                transfer.ConversationId,
                transfer.FromUserId
            })
            .HasPrincipalKey(participant => new
            {
                participant.Id,
                participant.ConversationId,
                participant.UserId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConversationParticipant>()
            .WithMany()
            .HasForeignKey(transfer => new
            {
                transfer.ToParticipantId,
                transfer.ConversationId,
                transfer.ToUserId
            })
            .HasPrincipalKey(participant => new
            {
                participant.Id,
                participant.ConversationId,
                participant.UserId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transfer => transfer.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(transfer => transfer.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
