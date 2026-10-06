using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams", "assessment", table =>
        {
            table.HasCheckConstraint("CK_Exams_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_Exams_CurrentVersionNumber", "[CurrentVersionNumber] > 0");
            table.HasCheckConstraint(
                "CK_Exams_PublicationState",
                "([Status] = 1 AND [LatestPublishedVersionNumber] IS NULL) OR ([Status] = 2 AND [LatestPublishedVersionNumber] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Exams_PublishedVersion",
                "[LatestPublishedVersionNumber] IS NULL OR ([LatestPublishedVersionNumber] > 0 AND [LatestPublishedVersionNumber] <= [CurrentVersionNumber])");
        });
        builder.HasKey(exam => exam.Id);
        builder.HasAlternateKey(exam => new { exam.OrganizationId, exam.Id })
            .HasName("UQ_Exams_OrganizationId_Id");
        builder.Property(exam => exam.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(exam => exam.RowVersion).IsRowVersion();
        builder.HasIndex(exam => new { exam.OrganizationId, exam.ClassId, exam.Status })
            .HasDatabaseName("IX_Exams_Organization_Class_Status");

        builder.HasOne<AcademicClass>()
            .WithMany()
            .HasForeignKey(exam => new { exam.OrganizationId, exam.ClassId })
            .HasPrincipalKey(academicClass => new { academicClass.OrganizationId, academicClass.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(exam => new { exam.OrganizationId, exam.CreatedByMembershipId })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
