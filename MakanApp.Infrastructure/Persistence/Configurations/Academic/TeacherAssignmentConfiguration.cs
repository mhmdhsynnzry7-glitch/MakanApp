using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
    {
        builder.ToTable("TeacherAssignments", "academic", table =>
            table.HasCheckConstraint("CK_TeacherAssignments_Status", "[Status] IN (1, 2)"));
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.AssignedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.RowVersion).IsRowVersion();

        builder.HasIndex(assignment => new
        {
            assignment.OrganizationId,
            assignment.ClassId,
            assignment.TeacherMembershipId
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_TeacherAssignments_Active_Organization_Class_Teacher");

        builder.HasOne<AcademicClass>()
            .WithMany()
            .HasForeignKey(assignment => new { assignment.OrganizationId, assignment.ClassId })
            .HasPrincipalKey(academicClass => new { academicClass.OrganizationId, academicClass.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(assignment => new
            {
                assignment.OrganizationId,
                assignment.TeacherMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
