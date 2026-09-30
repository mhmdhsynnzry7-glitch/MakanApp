using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class AttendanceRevisionConfiguration : IEntityTypeConfiguration<AttendanceRevision>
{
    public void Configure(EntityTypeBuilder<AttendanceRevision> builder)
    {
        builder.ToTable("AttendanceRevisions", "academic", table =>
        {
            table.HasCheckConstraint("CK_AttendanceRevisions_PreviousStatus", "[PreviousStatus] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_AttendanceRevisions_NewStatus", "[NewStatus] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_AttendanceRevisions_StatusChanged", "[PreviousStatus] <> [NewStatus]");
        });
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.CorrectedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(revision => revision.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne<Attendance>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.ClassId,
                revision.SessionId,
                revision.EnrollmentId,
                revision.AttendanceId
            })
            .HasPrincipalKey(attendance => new
            {
                attendance.OrganizationId,
                attendance.ClassId,
                attendance.SessionId,
                attendance.EnrollmentId,
                attendance.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(revision => new
            {
                revision.OrganizationId,
                revision.CorrectedByMembershipId
            })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
