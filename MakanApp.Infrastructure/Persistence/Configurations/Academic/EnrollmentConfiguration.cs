using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments", "academic", table =>
            table.HasCheckConstraint("CK_Enrollments_Status", "[Status] IN (1, 2, 3)"));
        builder.HasKey(enrollment => enrollment.Id);
        builder.HasAlternateKey(enrollment => new
        {
            enrollment.OrganizationId,
            enrollment.ClassId,
            enrollment.Id
        }).HasName("UQ_Enrollments_OrganizationId_ClassId_Id");
        builder.Property(enrollment => enrollment.EnrolledAtUtc).HasColumnType("datetime2(7)");
        builder.Property(enrollment => enrollment.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(enrollment => enrollment.RowVersion).IsRowVersion();

        builder.HasIndex(enrollment => new
        {
            enrollment.OrganizationId,
            enrollment.ClassId,
            enrollment.LearnerOrganizationPersonId
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_Enrollments_Active_Organization_Class_Learner");

        builder.HasOne<AcademicClass>()
            .WithMany()
            .HasForeignKey(enrollment => new { enrollment.OrganizationId, enrollment.ClassId })
            .HasPrincipalKey(academicClass => new { academicClass.OrganizationId, academicClass.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationPerson>()
            .WithMany()
            .HasForeignKey(enrollment => new
            {
                enrollment.OrganizationId,
                enrollment.LearnerOrganizationPersonId
            })
            .HasPrincipalKey(person => new { person.OrganizationId, person.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
