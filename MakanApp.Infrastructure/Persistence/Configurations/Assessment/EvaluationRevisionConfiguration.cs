using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class EvaluationRevisionConfiguration : IEntityTypeConfiguration<EvaluationRevision>
{
    public void Configure(EntityTypeBuilder<EvaluationRevision> builder)
    {
        builder.ToTable("EvaluationRevisions", "assessment", table =>
        {
            table.HasCheckConstraint("CK_EvaluationRevisions_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_EvaluationRevisions_RevisionNumber", "[RevisionNumber] > 0");
            table.HasCheckConstraint("CK_EvaluationRevisions_Score", "[Score] >= 0");
            table.HasCheckConstraint(
                "CK_EvaluationRevisions_Correction",
                "([RevisionNumber] = 1 AND [SupersedesEvaluationRevisionId] IS NULL AND [CorrectionReason] IS NULL) OR " +
                "([RevisionNumber] > 1 AND [SupersedesEvaluationRevisionId] IS NOT NULL AND [CorrectionReason] IS NOT NULL)");
        });

        builder.HasKey(evaluation => evaluation.Id);
        builder.HasAlternateKey(evaluation => new
        {
            evaluation.OrganizationId,
            evaluation.SubmissionAttemptId,
            evaluation.Id
        }).HasName("UQ_EvaluationRevisions_Organization_Attempt_Id");
        builder.Property(evaluation => evaluation.Score).HasPrecision(9, 2);
        builder.Property(evaluation => evaluation.LearnerFeedback)
            .HasMaxLength(EvaluationRevision.MaximumFeedbackLength);
        builder.Property(evaluation => evaluation.GuardianVisibleFeedback)
            .HasMaxLength(EvaluationRevision.MaximumFeedbackLength);
        builder.Property(evaluation => evaluation.TeacherPrivateNote)
            .HasMaxLength(EvaluationRevision.MaximumPrivateNoteLength);
        builder.Property(evaluation => evaluation.CorrectionReason)
            .HasMaxLength(EvaluationRevision.MaximumCorrectionReasonLength);
        builder.Property(evaluation => evaluation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(evaluation => evaluation.UpdatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(evaluation => evaluation.RowVersion).IsRowVersion();

        builder.HasIndex(evaluation => new
        {
            evaluation.OrganizationId,
            evaluation.SubmissionAttemptId,
            evaluation.RevisionNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_EvaluationRevisions_Attempt_RevisionNumber");
        builder.HasIndex(evaluation => new
        {
            evaluation.OrganizationId,
            evaluation.SubmissionAttemptId,
            evaluation.Status
        })
            .IsUnique()
            .HasFilter("[Status] IN (1, 2)")
            .HasDatabaseName("UX_EvaluationRevisions_OneCurrentStatus");

        builder.HasOne<SubmissionAttempt>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.OrganizationId,
                evaluation.SubmissionAttemptId
            })
            .HasPrincipalKey(attempt => new { attempt.OrganizationId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvaluationRevision>()
            .WithMany()
            .HasForeignKey(evaluation => new
            {
                evaluation.OrganizationId,
                evaluation.SubmissionAttemptId,
                evaluation.SupersedesEvaluationRevisionId
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
            .HasForeignKey(evaluation => new
            {
                evaluation.OrganizationId,
                evaluation.CreatedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
