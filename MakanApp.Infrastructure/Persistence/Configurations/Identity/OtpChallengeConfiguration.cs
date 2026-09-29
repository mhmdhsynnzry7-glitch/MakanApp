using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges", "identity");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.NormalizedPhoneNumber)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(challenge => challenge.CodeHash).HasMaxLength(32).IsRequired();
        builder.Property(challenge => challenge.Salt).HasMaxLength(16).IsRequired();
        builder.Property(challenge => challenge.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(challenge => challenge.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(challenge => challenge.ConsumedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(challenge => challenge.SupersededAtUtc).HasColumnType("datetime2(7)");
        builder.Property(challenge => challenge.RowVersion).IsRowVersion();

        builder.HasIndex(challenge => new
        {
            challenge.NormalizedPhoneNumber,
            challenge.CreatedAtUtc
        })
            .HasDatabaseName("IX_OtpChallenges_Phone_CreatedAtUtc");
    }
}
