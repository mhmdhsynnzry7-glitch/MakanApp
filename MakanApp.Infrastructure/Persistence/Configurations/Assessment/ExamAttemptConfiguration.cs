using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
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
            table.HasCheckConstraint("CK_ExamAttempts_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_ExamAttempts_Deadline",
                "[EffectiveDeadlineUtc] > [StartedAtUtc]");
            table.HasCheckConstraint(
                "CK_ExamAttempts_ExpirationState",
                "([Status] = 1 AND [ExpiredAtUtc] IS NULL) OR ([Status] = 2 AND [ExpiredAtUtc] IS NOT NULL)");
        });
        builder.HasKey(attempt => attempt.Id);
        builder.HasAlternateKey(attempt => new
        {
            attempt.OrganizationId,
            attempt.ExamVersionId,
            attempt.Id
        }).HasName("UQ_ExamAttempts_Organization_ExamVersion_Id");
        builder.Property(attempt => attempt.StartedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.EffectiveDeadlineUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attempt => attempt.ExpiredAtUtc).HasColumnType("datetime2(7)");
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
    }
}
