using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", "organization", table =>
        {
            table.HasCheckConstraint("CK_Invitations_Role", "[Role] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Invitations_Status", "[Status] IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint(
                "CK_Invitations_Expiry",
                "[ExpiresAtUtc] > [CreatedAtUtc]");
        });
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.ExpiresAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.AcceptedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.DeclinedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.RevokedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(invitation => invitation.RowVersion).IsRowVersion();

        builder.HasIndex(invitation => new
        {
            invitation.DestinationUserId,
            invitation.OrganizationId,
            invitation.Role
        })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_Invitations_Pending_Destination_Organization_Role");

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(invitation => invitation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.DestinationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedMembershipId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoleAssignment>()
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedRoleAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
