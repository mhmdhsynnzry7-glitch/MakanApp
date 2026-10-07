using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamGradeRevisionConfiguration : IEntityTypeConfiguration<ExamGradeRevision>
{
    public void Configure(EntityTypeBuilder<ExamGradeRevision> builder)
    {
        builder.ToTable("ExamGradeRevisions", "assessment", table =>
        {
            table.HasCheckConstraint(
                "CK_ExamGradeRevisions_Status",
                "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_ExamGradeRevisions_RevisionNumber",
                "[RevisionNumber] > 0");
            table.HasCheckConstraint(
                "CK_ExamGradeRevisions_TotalScore",
                "[TotalScore] >= 0");
            table.HasCheckConstraint(
                "CK_ExamGradeRevisions_Correction",
                "([RevisionNumber] = 1 AND [SupersedesExamGradeRevisionId] IS NULL AND [CorrectionReason] IS NULL) OR " +
                "([RevisionNumber] > 1 AND [SupersedesExamGradeRevisionId] IS NOT NULL AND [CorrectionReason] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ExamGradeRevisions_UpdatedAt",
                "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(revision => revision.Id);
        builder.HasAlternateKey(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId,
            revision.Id
        }).HasName("UQ_ExamGradeRevisions_Organization_Attempt_Id");
        builder.Property(revision => revision.TotalScore).HasPrecision(9, 2);
        builder.Property(revision => revision.LearnerFeedback)
            .HasMaxLength(ExamGradeRevision.MaximumFeedbackLength);
        builder.Property(revision => revision.GuardianVisibleFeedback)
            .HasMaxLength(ExamGradeRevision.MaximumFeedbackLength);
        builder.Property(revision => revision.EvaluatorPrivateNote)
            .HasMaxLength(ExamGradeRevision.MaximumPrivateNoteLength);
        builder.Property(revision => revision.CorrectionReason)
            .HasMaxLength(ExamGradeRevision.MaximumCorrectionReasonLength);
        builder.Property(revision => revision.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(revision => revision.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(revision => revision.RowVersion).IsRowVersion();

        builder.HasIndex(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId,
            revision.RevisionNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamGradeRevisions_Attempt_RevisionNumber");
        builder.HasIndex(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId
        })
            .IsUnique()
            .HasFilter("[Status] IN (1, 2)")
            .HasDatabaseName("UX_ExamGradeRevisions_OneMutable");
        builder.HasIndex(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId,
            revision.Status
        })
            .IsUnique()
            .HasFilter("[Status] = 3")
            .HasDatabaseName("UX_ExamGradeRevisions_OneReleased");

        builder.HasOne<ExamAttempt>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId
            })
            .HasPrincipalKey(attempt => new { attempt.OrganizationId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamGradeRevision>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId,
                revision.SupersedesExamGradeRevisionId
            })
            .HasPrincipalKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId,
                Id = (Guid?)revision.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.CreatedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
