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
    }
}
