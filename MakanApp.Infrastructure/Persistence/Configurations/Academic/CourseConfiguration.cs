using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", "academic", table =>
            table.HasCheckConstraint("CK_Courses_Status", "[Status] IN (1, 2)"));
        builder.HasKey(course => course.Id);
        builder.HasAlternateKey(course => new { course.OrganizationId, course.Id })
            .HasName("UQ_Courses_OrganizationId_Id");
        builder.Property(course => course.Title).HasMaxLength(200).IsRequired();
        builder.Property(course => course.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(course => course.RowVersion).IsRowVersion();

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(course => course.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
