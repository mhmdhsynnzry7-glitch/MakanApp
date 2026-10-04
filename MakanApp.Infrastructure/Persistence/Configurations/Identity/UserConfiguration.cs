using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "identity", table =>
            table.HasCheckConstraint(
                "CK_Users_CommunicationAgeCategory",
                "[CommunicationAgeCategory] IN (0, 1, 2)"));
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Username).HasMaxLength(32);
        builder.Property(user => user.NormalizedUsername).HasMaxLength(32);
        builder.Property(user => user.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(user => user.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(user => user.ProfileCompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(user => user.RowVersion).IsRowVersion();

        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique()
            .HasFilter("[NormalizedUsername] IS NOT NULL")
            .HasDatabaseName("UX_Users_NormalizedUsername");

        builder.HasIndex(user => user.PersonId)
            .IsUnique()
            .HasFilter("[PersonId] IS NOT NULL");

        builder.HasOne<Person>()
            .WithOne()
            .HasForeignKey<User>(user => user.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
