using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class SubmissionAttemptConfiguration : IEntityTypeConfiguration<SubmissionAttempt>
{
    public void Configure(EntityTypeBuilder<SubmissionAttempt> builder)
    {
        builder.ToTable("SubmissionAttempts", "assessment", table =>
        {
            table.HasCheckConstraint("CK_SubmissionAttempts_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_SubmissionAttempts_AttemptNumber", "[AttemptNumber] > 0");
            table.HasCheckConstraint(
                "CK_SubmissionAttempts_State",
                "([Status] = 1 AND [SubmittedAtUtc] IS NULL AND [IsLate] = 0) OR ([Status] = 2 AND [SubmittedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_SubmissionAttempts_Timestamps",
                "[LastSavedAtUtc] IS NULL OR [LastSavedAtUtc] >= [CreatedAtUtc]");
        });

        builder.HasKey(attempt => attempt.Id);
        builder.HasAlternateKey(attempt => new { attempt.OrganizationId, attempt.Id })
            .HasName("UQ_SubmissionAttempts_Organization_Id");
        builder.Property(attempt => attempt.AnswerText)
            .HasMaxLength(SubmissionAttempt.MaximumAnswerLength);
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.LastSavedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.SubmittedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.RowVersion).IsRowVersion();

        builder.HasIndex(attempt => new
        {
            attempt.OrganizationId,
            attempt.AssignmentRecipientId,
            attempt.AssignmentVersionId,
            attempt.AttemptNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_SubmissionAttempts_Recipient_Version_Number");
        builder.HasIndex(attempt => new
        {
            attempt.OrganizationId,
            attempt.AssignmentRecipientId,
            attempt.AssignmentVersionId
        })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_SubmissionAttempts_OneDraft");
        builder.HasIndex(attempt => new
        {
            attempt.OrganizationId,
            attempt.AssignmentId,
            attempt.Status,
            attempt.SubmittedAtUtc
        }).HasDatabaseName("IX_SubmissionAttempts_Assignment_Status_SubmittedAtUtc");

        builder.HasOne<AssignmentVersion>()
            .WithMany()
            .HasForeignKey(attempt => new
            {
                attempt.OrganizationId,
                attempt.AssignmentId,
                attempt.AssignmentVersionId
            })
            .HasPrincipalKey(version => new
            {
                version.OrganizationId,
                version.AssignmentId,
                version.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssignmentRecipient>()
            .WithMany()
            .HasForeignKey(attempt => new
            {
                attempt.OrganizationId,
                attempt.AssignmentId,
                attempt.AssignmentVersionId,
                attempt.EnrollmentId,
                attempt.AssignmentRecipientId
            })
            .HasPrincipalKey(recipient => new
            {
                recipient.OrganizationId,
                recipient.AssignmentId,
                recipient.AssignmentVersionId,
                recipient.EnrollmentId,
                recipient.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
