using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments", "assessment", table =>
        {
            table.HasCheckConstraint("CK_Assignments_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_Assignments_CurrentVersionNumber", "[CurrentVersionNumber] > 0");
            table.HasCheckConstraint(
                "CK_Assignments_PublicationState",
                "([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] IN (2, 3, 4) AND [PublishedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_Assignments_UpdatedAt", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(assignment => assignment.Id);
        builder.HasAlternateKey(assignment => new
        {
            assignment.OrganizationId,
            assignment.ClassId,
            assignment.Id
        }).HasName("UQ_Assignments_OrganizationId_ClassId_Id");
        builder.Property(assignment => assignment.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.PublishedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(assignment => assignment.RowVersion).IsRowVersion();
        builder.HasIndex(assignment => new
        {
            assignment.OrganizationId,
            assignment.ClassId,
            assignment.Status,
            assignment.UpdatedAtUtc
        }).HasDatabaseName("IX_Assignments_Organization_Class_Status_UpdatedAt");

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
                assignment.CreatedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
