using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("UserBlocks", "messaging", table =>
        {
            table.HasCheckConstraint("CK_UserBlocks_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_UserBlocks_DistinctUsers",
                "[BlockerUserId] <> [BlockedUserId]");
            table.HasCheckConstraint(
                "CK_UserBlocks_Lifecycle",
                "([Status] = 1 AND [EndedAtUtc] IS NULL) OR " +
                "([Status] = 2 AND [EndedAtUtc] IS NOT NULL AND [EndedAtUtc] >= [CreatedAtUtc])");
        });
        builder.HasKey(block => block.Id);
        builder.Property(block => block.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(block => block.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(block => block.RowVersion).IsRowVersion();
        builder.HasIndex(block => new { block.BlockerUserId, block.BlockedUserId })
            .IsUnique()
            .HasFilter("[EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_UserBlocks_Active_DirectionalPair");
        builder.HasIndex(block => new { block.BlockerUserId, block.CreatedAtUtc })
            .HasDatabaseName("IX_UserBlocks_Blocker_CreatedAtUtc");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
