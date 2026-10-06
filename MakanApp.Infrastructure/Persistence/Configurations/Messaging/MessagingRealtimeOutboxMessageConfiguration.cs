using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class MessagingRealtimeOutboxMessageConfiguration :
    IEntityTypeConfiguration<MessagingRealtimeOutboxMessage>
{
    public void Configure(EntityTypeBuilder<MessagingRealtimeOutboxMessage> builder)
    {
        builder.ToTable("RealtimeOutbox", "messaging", table =>
        {
            table.HasCheckConstraint("CK_RealtimeOutbox_AttemptCount", "[AttemptCount] >= 0");
            table.HasCheckConstraint(
                "CK_RealtimeOutbox_Lifecycle",
                "[DispatchedAtUtc] IS NULL OR ([ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)");
        });
        builder.HasKey(message => message.Id);
        builder.Property(message => message.OccurredAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.ClaimedUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.NextAttemptAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.DispatchedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(message => message.RowVersion).IsRowVersion();
        builder.HasIndex(message => message.ChangeEventId)
            .IsUnique()
            .HasDatabaseName("UX_RealtimeOutbox_ChangeEvent");
        builder.HasIndex(message => new
        {
            message.DispatchedAtUtc,
            message.NextAttemptAtUtc,
            message.ClaimedUntilUtc,
            message.OccurredAtUtc
        })
            .HasFilter("[DispatchedAtUtc] IS NULL")
            .HasDatabaseName("IX_RealtimeOutbox_Pending");
        builder.HasOne<MessagingChangeEvent>()
            .WithMany()
            .HasForeignKey(message => message.ChangeEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
