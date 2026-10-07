using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamAttemptQuestionConfiguration : IEntityTypeConfiguration<ExamAttemptQuestion>
{
    public void Configure(EntityTypeBuilder<ExamAttemptQuestion> builder)
    {
        builder.ToTable("ExamAttemptQuestions", "assessment", table =>
            table.HasCheckConstraint("CK_ExamAttemptQuestions_DisplayOrder", "[DisplayOrder] > 0"));
        builder.HasKey(mapping => mapping.Id);
        builder.HasAlternateKey(mapping => new
        {
            mapping.OrganizationId,
            mapping.ExamAttemptId,
            mapping.Id,
            mapping.QuestionVersionId
        }).HasName("UQ_ExamAttemptQuestions_Organization_Attempt_Id_QuestionVersion");
        builder.Property(mapping => mapping.RowVersion).IsRowVersion();
        builder.HasIndex(mapping => new
        {
            mapping.OrganizationId,
            mapping.ExamAttemptId,
            mapping.DisplayOrder
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttemptQuestions_Organization_Attempt_DisplayOrder");
        builder.HasIndex(mapping => new
        {
            mapping.OrganizationId,
            mapping.ExamAttemptId,
            mapping.QuestionVersionId
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttemptQuestions_Organization_Attempt_Question");

        builder.HasOne<ExamAttempt>()
            .WithMany()
            .HasForeignKey(mapping => new
            {
                mapping.OrganizationId,
                mapping.ExamVersionId,
                mapping.ExamAttemptId
            })
            .HasPrincipalKey(attempt => new
            {
                attempt.OrganizationId,
                attempt.ExamVersionId,
                attempt.Id
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<QuestionVersion>()
            .WithMany()
            .HasForeignKey(mapping => new
            {
                mapping.OrganizationId,
                mapping.ExamVersionId,
                mapping.QuestionVersionId
            })
            .HasPrincipalKey(question => new
            {
                question.OrganizationId,
                question.ExamVersionId,
                question.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnswerRevision>()
            .WithMany()
            .HasForeignKey(mapping => new
            {
                mapping.OrganizationId,
                mapping.ExamAttemptId,
                ExamAttemptQuestionId = mapping.Id,
                mapping.CurrentAnswerRevisionId
            })
            .HasPrincipalKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId,
                revision.ExamAttemptQuestionId,
                Id = (Guid?)revision.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
