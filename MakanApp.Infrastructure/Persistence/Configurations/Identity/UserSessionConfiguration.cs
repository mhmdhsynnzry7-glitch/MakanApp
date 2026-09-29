using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions", "identity");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(session => session.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.RevokedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.SelectedRole).HasMaxLength(16);
        builder.Property(session => session.RowVersion).IsRowVersion();

        builder.HasIndex(session => session.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_UserSessions_TokenHash");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
