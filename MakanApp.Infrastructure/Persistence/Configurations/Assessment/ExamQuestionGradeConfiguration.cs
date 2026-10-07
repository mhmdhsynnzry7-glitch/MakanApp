using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamQuestionGradeConfiguration : IEntityTypeConfiguration<ExamQuestionGrade>
{
    public void Configure(EntityTypeBuilder<ExamQuestionGrade> builder)
    {
        builder.ToTable("ExamQuestionGrades", "assessment", table =>
        {
            table.HasCheckConstraint(
                "CK_ExamQuestionGrades_GradingMode",
                "[GradingMode] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_ExamQuestionGrades_MaximumScore",
                "[MaximumScore] > 0");
            table.HasCheckConstraint(
                "CK_ExamQuestionGrades_AwardedScore",
                "[AwardedScore] >= 0 AND [AwardedScore] <= [MaximumScore]");
            table.HasCheckConstraint(
                "CK_ExamQuestionGrades_ReviewState",
                "([IsReviewed] = 0 AND [ReviewedByMembershipId] IS NULL AND [ReviewedAtUtc] IS NULL) OR " +
                "([IsReviewed] = 1 AND [ReviewedByMembershipId] IS NOT NULL AND [ReviewedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ExamQuestionGrades_ObjectiveReviewed",
                "[GradingMode] <> 1 OR [IsReviewed] = 1");
        });
        builder.HasKey(grade => grade.Id);
        builder.Property(grade => grade.MaximumScore).HasPrecision(9, 2);
        builder.Property(grade => grade.AwardedScore).HasPrecision(9, 2);
        builder.Property(grade => grade.LearnerFeedback)
            .HasMaxLength(ExamQuestionGrade.MaximumFeedbackLength);
        builder.Property(grade => grade.EvaluatorPrivateNote)
            .HasMaxLength(ExamQuestionGrade.MaximumPrivateNoteLength);
        builder.Property(grade => grade.ReviewedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(grade => new
        {
            grade.OrganizationId,
            grade.ExamAttemptId,
            grade.ExamGradeRevisionId,
            grade.ExamAttemptQuestionId
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamQuestionGrades_Revision_AttemptQuestion");

        builder.HasOne<ExamGradeRevision>()
            .WithMany()
            .HasForeignKey(grade => new
            {
                grade.OrganizationId,
                grade.ExamAttemptId,
                Id = grade.ExamGradeRevisionId
            })
            .HasPrincipalKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId,
                revision.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamAttemptQuestion>()
            .WithMany()
            .HasForeignKey(grade => new
            {
                grade.OrganizationId,
                grade.ExamAttemptId,
                Id = grade.ExamAttemptQuestionId,
                grade.QuestionVersionId
            })
            .HasPrincipalKey(question => new
            {
                question.OrganizationId,
                question.ExamAttemptId,
                question.Id,
                question.QuestionVersionId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(grade => new
            {
                grade.OrganizationId,
                Id = grade.ReviewedByMembershipId
            })
            .HasPrincipalKey(membership => new
            {
                membership.OrganizationId,
                Id = (Guid?)membership.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
