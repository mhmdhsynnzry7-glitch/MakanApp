using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendance", "academic", table =>
            table.HasCheckConstraint("CK_Attendance_Status", "[Status] IN (1, 2, 3, 4)"));
        builder.HasKey(attendance => attendance.Id);
        builder.HasAlternateKey(attendance => new
        {
            attendance.OrganizationId,
            attendance.ClassId,
            attendance.SessionId,
            attendance.EnrollmentId,
            attendance.Id
        }).HasName("UQ_Attendance_Context_Id");
        builder.Property(attendance => attendance.RecordedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(attendance => attendance.RowVersion).IsRowVersion();
        builder.HasIndex(attendance => new
        {
            attendance.OrganizationId,
            attendance.SessionId,
            attendance.EnrollmentId
        })
            .IsUnique()
            .HasDatabaseName("UX_Attendance_Organization_Session_Enrollment");

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(attendance => new
            {
                attendance.OrganizationId,
                attendance.ClassId,
                attendance.SessionId
            })
            .HasPrincipalKey(session => new { session.OrganizationId, session.ClassId, session.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>()
            .WithMany()
            .HasForeignKey(attendance => new
            {
                attendance.OrganizationId,
                attendance.ClassId,
                attendance.EnrollmentId
            })
            .HasPrincipalKey(enrollment => new
            {
                enrollment.OrganizationId,
                enrollment.ClassId,
                enrollment.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(attendance => new
            {
                attendance.OrganizationId,
                attendance.RecordedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
