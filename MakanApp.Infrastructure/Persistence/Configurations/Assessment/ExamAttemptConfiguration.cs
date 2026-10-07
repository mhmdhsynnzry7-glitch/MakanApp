using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("ExamAttempts", "assessment", table =>
        {
            table.HasCheckConstraint("CK_ExamAttempts_AttemptNumber", "[AttemptNumber] > 0");
            table.HasCheckConstraint("CK_ExamAttempts_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_ExamAttempts_WriteLeaseVersion", "[WriteLeaseVersion] >= 0");
            table.HasCheckConstraint("CK_ExamAttempts_AnswerSetVersion", "[AnswerSetVersion] >= 0");
            table.HasCheckConstraint(
                "CK_ExamAttempts_WriteLeaseState",
                "([WriterSessionId] IS NULL AND [WriteLeaseVersion] = 0 AND [WriteLeaseAcquiredAtUtc] IS NULL) OR ([WriterSessionId] IS NOT NULL AND [WriteLeaseVersion] > 0 AND [WriteLeaseAcquiredAtUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ExamAttempts_Deadline",
                "[EffectiveDeadlineUtc] > [StartedAtUtc]");
            table.HasCheckConstraint(
                "CK_ExamAttempts_ExpirationState",
                "([Status] IN (1, 3) AND [ExpiredAtUtc] IS NULL) OR ([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ExamAttempts_FinalizationState",
                "([Status] = 3 AND [FinalizedAtUtc] IS NOT NULL AND [FinalizedAnswerSetVersion] IS NOT NULL AND [FinalizeClientOperationId] IS NOT NULL AND [FinalizeRequestHash] IS NOT NULL AND [FinalizedBySessionId] IS NOT NULL) OR ([Status] <> 3 AND [FinalizedAtUtc] IS NULL AND [FinalizedAnswerSetVersion] IS NULL AND [FinalizeClientOperationId] IS NULL AND [FinalizeRequestHash] IS NULL AND [FinalizedBySessionId] IS NULL)");
            table.HasCheckConstraint(
                "CK_ExamAttempts_FinalizedAnswerSetVersion",
                "[FinalizedAnswerSetVersion] IS NULL OR ([FinalizedAnswerSetVersion] >= 0 AND [FinalizedAnswerSetVersion] = [AnswerSetVersion])");
            table.HasCheckConstraint(
                "CK_ExamAttempts_FinalizedBeforeDeadline",
                "[FinalizedAtUtc] IS NULL OR [FinalizedAtUtc] < [EffectiveDeadlineUtc]");
            table.HasCheckConstraint(
                "CK_ExamAttempts_FinalizeRequestHash",
                $"[FinalizeRequestHash] IS NULL OR LEN([FinalizeRequestHash]) = {ExamAttempt.FinalizeRequestHashLength}");
        });
        builder.HasKey(attempt => attempt.Id);
        builder.HasAlternateKey(attempt => new
        {
            attempt.OrganizationId,
            attempt.ExamVersionId,
            attempt.Id
        }).HasName("UQ_ExamAttempts_Organization_ExamVersion_Id");
        builder.HasAlternateKey(attempt => new { attempt.OrganizationId, attempt.Id })
            .HasName("UQ_ExamAttempts_Organization_Id");
        builder.Property(attempt => attempt.StartedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.EffectiveDeadlineUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.ExpiredAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.WriteLeaseAcquiredAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.FinalizedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.FinalizeRequestHash)
            .HasMaxLength(ExamAttempt.FinalizeRequestHashLength)
            .IsUnicode(false);
        builder.Property(attempt => attempt.RowVersion).IsRowVersion();

        builder.HasIndex(attempt => new
        {
            attempt.OrganizationId,
            attempt.ExamId,
            attempt.EnrollmentId,
            attempt.AttemptNumber
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttempts_Organization_Exam_Enrollment_Number");
        builder.HasIndex(attempt => new
        {
            attempt.OrganizationId,
            attempt.ExamId,
            attempt.EnrollmentId
        })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_ExamAttempts_Organization_Exam_Enrollment_Active");
        builder.HasIndex(attempt => new
        {
            attempt.OrganizationId,
            attempt.EnrollmentId,
            attempt.ClientOperationId
        })
            .IsUnique()
            .HasDatabaseName("UX_ExamAttempts_Organization_Enrollment_ClientOperation");

        builder.HasOne<Exam>()
            .WithMany()
            .HasForeignKey(attempt => new
            {
                attempt.OrganizationId,
                attempt.ClassId,
                attempt.ExamId
            })
            .HasPrincipalKey(exam => new { exam.OrganizationId, exam.ClassId, exam.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamVersion>()
            .WithMany()
            .HasForeignKey(attempt => new
            {
                attempt.OrganizationId,
                attempt.ExamId,
                attempt.ExamVersionId
            })
            .HasPrincipalKey(version => new { version.OrganizationId, version.ExamId, version.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>()
            .WithMany()
            .HasForeignKey(attempt => new
            {
                attempt.OrganizationId,
                attempt.ClassId,
                attempt.EnrollmentId
            })
            .HasPrincipalKey(enrollment => new
            {
                enrollment.OrganizationId,
                enrollment.ClassId,
                enrollment.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(attempt => attempt.WriterSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(attempt => attempt.FinalizedBySessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
