using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class AnswerRevisionConfiguration : IEntityTypeConfiguration<AnswerRevision>
{
    public void Configure(EntityTypeBuilder<AnswerRevision> builder)
    {
        builder.ToTable("AnswerRevisions", "assessment", table =>
        {
            table.HasCheckConstraint("CK_AnswerRevisions_RevisionNumber", "[RevisionNumber] > 0");
            table.HasCheckConstraint("CK_AnswerRevisions_AnswerSetVersion", "[AcceptedAnswerSetVersion] > 0");
            table.HasCheckConstraint("CK_AnswerRevisions_WriteLeaseVersion", "[AcceptedWriteLeaseVersion] > 0");
            table.HasCheckConstraint("CK_AnswerRevisions_AnswerType", "[AnswerType] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_AnswerRevisions_AnswerShape",
                "([AnswerType] = 1 AND [SelectedOptionId] IS NOT NULL AND [TextAnswer] IS NULL) OR ([AnswerType] = 2 AND [SelectedOptionId] IS NULL AND [TextAnswer] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_AnswerRevisions_TextLength",
                $"[TextAnswer] IS NULL OR DATALENGTH([TextAnswer]) <= {AnswerRevision.MaximumTextAnswerLength * 2}");
            table.HasCheckConstraint("CK_AnswerRevisions_RequestHash", "LEN([RequestHash]) = 64");
        });
        builder.HasKey(revision => revision.Id);
        builder.HasAlternateKey(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId,
            revision.ExamAttemptQuestionId,
            revision.Id
        }).HasName("UQ_AnswerRevisions_Organization_Attempt_Question_Id");
        builder.Property(revision => revision.TextAnswer)
            .HasMaxLength(AnswerRevision.MaximumTextAnswerLength);
        builder.Property(revision => revision.RequestHash)
            .HasMaxLength(AnswerRevision.RequestHashLength)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(revision => revision.AcceptedAtUtc).HasColumnType("datetime2(7)");

        builder.HasIndex(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId,
            revision.ExamAttemptQuestionId,
            revision.RevisionNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_AnswerRevisions_Organization_AttemptQuestion_Revision");
        builder.HasIndex(revision => new
        {
            revision.OrganizationId,
            revision.ExamAttemptId,
            revision.ClientOperationId
        })
            .IsUnique()
            .HasDatabaseName("UX_AnswerRevisions_Organization_Attempt_ClientOperation");

        builder.HasOne<ExamAttempt>()
            .WithMany()
            .HasForeignKey(revision => new { revision.OrganizationId, revision.ExamAttemptId })
            .HasPrincipalKey(attempt => new { attempt.OrganizationId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamAttemptQuestion>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId,
                revision.ExamAttemptQuestionId,
                revision.QuestionVersionId
            })
            .HasPrincipalKey(question => new
            {
                question.OrganizationId,
                question.ExamAttemptId,
                question.Id,
                question.QuestionVersionId
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QuestionOption>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.QuestionVersionId,
                revision.SelectedOptionId
            })
            .HasPrincipalKey(option => new
            {
                option.OrganizationId,
                option.QuestionVersionId,
                Id = (Guid?)option.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(revision => revision.CreatedBySessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnswerRevision>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.ExamAttemptId,
                revision.ExamAttemptQuestionId,
                revision.SupersedesAnswerRevisionId
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
