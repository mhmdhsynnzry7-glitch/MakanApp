using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class GradeReleaseConfiguration : IEntityTypeConfiguration<GradeRelease>
{
    public void Configure(EntityTypeBuilder<GradeRelease> builder)
    {
        builder.ToTable("GradeReleases", "assessment");
        builder.HasKey(release => release.Id);
        builder.Property(release => release.ReleasedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(release => new
        {
            release.OrganizationId,
            release.EvaluationRevisionId
        })
            .IsUnique()
            .HasDatabaseName("UX_GradeReleases_Organization_EvaluationRevision");
        builder.HasIndex(release => new
        {
            release.OrganizationId,
            release.SubmissionAttemptId,
            release.ReleasedAtUtc
        }).HasDatabaseName("IX_GradeReleases_Attempt_ReleasedAtUtc");

        builder.HasOne<EvaluationRevision>()
            .WithMany()
            .HasForeignKey(release => new
            {
                release.OrganizationId,
                release.SubmissionAttemptId,
                release.EvaluationRevisionId
            })
            .HasPrincipalKey(evaluation => new
            {
                evaluation.OrganizationId,
                evaluation.SubmissionAttemptId,
                evaluation.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(release => new
            {
                release.OrganizationId,
                release.ReleasedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
