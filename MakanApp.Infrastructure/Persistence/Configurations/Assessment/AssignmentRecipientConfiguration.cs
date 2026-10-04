using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Assessment;

public sealed class AssignmentRecipientConfiguration : IEntityTypeConfiguration<AssignmentRecipient>
{
    public void Configure(EntityTypeBuilder<AssignmentRecipient> builder)
    {
        builder.ToTable("AssignmentRecipients", "assessment");
        builder.HasKey(recipient => recipient.Id);
        builder.Property(recipient => recipient.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(recipient => new
        {
            recipient.OrganizationId,
            recipient.AssignmentVersionId,
            recipient.EnrollmentId
        })
            .IsUnique()
            .HasDatabaseName("UX_AssignmentRecipients_Organization_Version_Enrollment");

        builder.HasOne<AssignmentVersion>()
            .WithMany()
            .HasForeignKey(recipient => new
            {
                recipient.OrganizationId,
                recipient.ClassId,
                recipient.AssignmentId,
                recipient.AssignmentVersionId
            })
            .HasPrincipalKey(version => new
            {
                version.OrganizationId,
                version.ClassId,
                version.AssignmentId,
                version.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>()
            .WithMany()
            .HasForeignKey(recipient => new
            {
                recipient.OrganizationId,
                recipient.ClassId,
                recipient.EnrollmentId
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
