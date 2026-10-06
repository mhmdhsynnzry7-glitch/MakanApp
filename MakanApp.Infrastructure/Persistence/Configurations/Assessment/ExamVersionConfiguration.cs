using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamVersionConfiguration : IEntityTypeConfiguration<ExamVersion>
{
    public void Configure(EntityTypeBuilder<ExamVersion> builder)
    {
        builder.ToTable("ExamVersions", "assessment", table =>
        {
            table.HasCheckConstraint("CK_ExamVersions_VersionNumber", "[VersionNumber] > 0");
            table.HasCheckConstraint("CK_ExamVersions_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_ExamVersions_Window", "[AvailableUntilUtc] > [AvailableFromUtc]");
            table.HasCheckConstraint("CK_ExamVersions_Duration", "[DurationMinutes] > 0");
            table.HasCheckConstraint("CK_ExamVersions_MaxAttempts", "[MaxAttempts] > 0");
            table.HasCheckConstraint("CK_ExamVersions_MaxScore", "[MaxScore] > 0");
            table.HasCheckConstraint("CK_ExamVersions_Randomization", "[RandomizationPolicy] IN (1, 2)");
            table.HasCheckConstraint("CK_ExamVersions_UpdatedAt", "[UpdatedAtUtc] >= [CreatedAtUtc]");
            table.HasCheckConstraint(
                "CK_ExamVersions_PublicationState",
                "([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] = 2 AND [PublishedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(version => version.Id);
        builder.HasAlternateKey(version => new { version.OrganizationId, version.Id })
            .HasName("UQ_ExamVersions_OrganizationId_Id");
        builder.HasAlternateKey(version => new
        {
            version.OrganizationId,
            version.ExamId,
            version.Id
        }).HasName("UQ_ExamVersions_Organization_Exam_Id");
        builder.Property(version => version.Title)
            .HasMaxLength(ExamVersion.MaximumTitleLength)
            .IsRequired();
        builder.Property(version => version.Description)
            .HasMaxLength(ExamVersion.MaximumDescriptionLength);
        builder.Property(version => version.AvailableFromUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.AvailableUntilUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.MaxScore).HasPrecision(9, 2);
        builder.Property(version => version.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.PublishedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(version => version.RowVersion).IsRowVersion();
        builder.HasIndex(version => new
        {
            version.OrganizationId,
            version.ExamId,
            version.VersionNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamVersions_Organization_Exam_VersionNumber");
        builder.HasIndex(version => new { version.OrganizationId, version.ExamId })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_ExamVersions_Organization_Exam_ActiveDraft");

        builder.HasOne<Exam>()
            .WithMany()
            .HasForeignKey(version => new { version.OrganizationId, version.ExamId })
            .HasPrincipalKey(exam => new { exam.OrganizationId, exam.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(version => new { version.OrganizationId, version.CreatedByMembershipId })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
