using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships", "organization", table =>
            table.HasCheckConstraint(
                "CK_Memberships_Status",
                "[Status] IN (1, 2, 3)"));
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(membership => membership.ActivatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(membership => membership.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(membership => membership.RowVersion).IsRowVersion();

        builder.HasIndex(membership => new
        {
            membership.UserId,
            membership.OrganizationId
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_Memberships_Active_User_Organization");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
