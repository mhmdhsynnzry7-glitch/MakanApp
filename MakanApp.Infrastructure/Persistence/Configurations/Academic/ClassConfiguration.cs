using AcademicClass = MakanApp.Domain.Academic.Class;
using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class ClassConfiguration : IEntityTypeConfiguration<AcademicClass>
{
    public void Configure(EntityTypeBuilder<AcademicClass> builder)
    {
        builder.ToTable("Classes", "academic", table =>
        {
            table.HasCheckConstraint("CK_Classes_Capacity", "[Capacity] > 0");
            table.HasCheckConstraint("CK_Classes_Status", "[Status] IN (1, 2, 3, 4)");
        });
        builder.HasKey(academicClass => academicClass.Id);
        builder.HasAlternateKey(academicClass => new
        {
            academicClass.OrganizationId,
            academicClass.Id
        }).HasName("UQ_Classes_OrganizationId_Id");
        builder.Property(academicClass => academicClass.Title).HasMaxLength(200).IsRequired();
        builder.Property(academicClass => academicClass.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(academicClass => academicClass.ActivatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(academicClass => academicClass.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(academicClass => academicClass.RowVersion).IsRowVersion();

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(academicClass => academicClass.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AcademicPeriod>()
            .WithMany()
            .HasForeignKey(academicClass => new
            {
                academicClass.OrganizationId,
                academicClass.AcademicPeriodId
            })
            .HasPrincipalKey(period => new { period.OrganizationId, period.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(academicClass => new
            {
                academicClass.OrganizationId,
                academicClass.CourseId
            })
            .HasPrincipalKey(course => new { course.OrganizationId, course.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
