using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments", "organization", table =>
        {
            table.HasCheckConstraint("CK_RoleAssignments_Role", "[Role] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_RoleAssignments_Status", "[Status] IN (1, 2)");
        });
        builder.HasKey(roleAssignment => roleAssignment.Id);
        builder.Property(roleAssignment => roleAssignment.AssignedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(roleAssignment => roleAssignment.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(roleAssignment => roleAssignment.RowVersion).IsRowVersion();

        builder.HasIndex(roleAssignment => new
        {
            roleAssignment.MembershipId,
            roleAssignment.Role
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_RoleAssignments_Active_Membership_Role");

        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(roleAssignment => roleAssignment.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
