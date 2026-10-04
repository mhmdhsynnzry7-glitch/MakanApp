using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class AssignmentVersionConfiguration : IEntityTypeConfiguration<AssignmentVersion>
{
    public void Configure(EntityTypeBuilder<AssignmentVersion> builder)
    {
        builder.ToTable("AssignmentVersions", "assessment", table =>
        {
            table.HasCheckConstraint("CK_AssignmentVersions_VersionNumber", "[VersionNumber] > 0");
            table.HasCheckConstraint("CK_AssignmentVersions_MaxAttempts", "[MaxAttempts] > 0");
            table.HasCheckConstraint("CK_AssignmentVersions_MaxScore", "[MaxScore] > 0");
            table.HasCheckConstraint("CK_AssignmentVersions_DueAt", "[DueAtUtc] > [CreatedAtUtc]");
            table.HasCheckConstraint(
                "CK_AssignmentVersions_PublishedDueAt",
                "[PublishedAtUtc] IS NULL OR [DueAtUtc] > [PublishedAtUtc]");
        });
        builder.HasKey(version => version.Id);
        builder.HasAlternateKey(version => new
        {
            version.OrganizationId,
            version.ClassId,
            version.AssignmentId,
            version.Id
        }).HasName("UQ_AssignmentVersions_Organization_Class_Assignment_Id");
        builder.HasAlternateKey(version => new
        {
            version.OrganizationId,
            version.AssignmentId,
            version.Id
        }).HasName("UQ_AssignmentVersions_Organization_Assignment_Id");
        builder.Property(version => version.Title)
            .HasMaxLength(AssignmentVersion.MaximumTitleLength)
            .IsRequired();
        builder.Property(version => version.Description)
            .HasMaxLength(AssignmentVersion.MaximumDescriptionLength)
            .IsRequired();
        builder.Property(version => version.MaxScore).HasPrecision(9, 2);
        builder.Property(version => version.DueAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.PublishedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.RowVersion).IsRowVersion();
        builder.HasIndex(version => new
        {
            version.OrganizationId,
            version.AssignmentId,
            version.VersionNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_AssignmentVersions_Organization_Assignment_VersionNumber");

        builder.HasOne<Assignment>()
            .WithMany()
            .HasForeignKey(version => new
            {
                version.OrganizationId,
                version.ClassId,
                version.AssignmentId
            })
            .HasPrincipalKey(assignment => new
            {
                assignment.OrganizationId,
                assignment.ClassId,
                assignment.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(version => new
            {
                version.OrganizationId,
                version.CreatedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
