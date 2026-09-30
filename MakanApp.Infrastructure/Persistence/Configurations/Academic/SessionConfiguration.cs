using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions", "academic", table =>
        {
            table.HasCheckConstraint("CK_Sessions_TimeRange", "[EndUtc] > [StartUtc]");
            table.HasCheckConstraint("CK_Sessions_Status", "[Status] IN (1, 2, 3)");
        });
        builder.HasKey(session => session.Id);
        builder.HasAlternateKey(session => new
        {
            session.OrganizationId,
            session.ClassId,
            session.Id
        }).HasName("UQ_Sessions_OrganizationId_ClassId_Id");
        builder.Property(session => session.Title).HasMaxLength(200).IsRequired();
        builder.Property(session => session.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(session => session.MeetingUrl).HasMaxLength(1000);
        builder.Property(session => session.StartUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.EndUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.CancelledAtUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.CompletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(session => session.RowVersion).IsRowVersion();

        builder.HasIndex(session => new
        {
            session.OrganizationId,
            session.ClassId,
            session.StartUtc,
            session.EndUtc
        }).HasDatabaseName("IX_Sessions_Organization_Class_Time");
        builder.HasIndex(session => new
        {
            session.OrganizationId,
            session.ScheduleRuleId,
            session.OccurrenceLocalDate
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [ScheduleRuleId] IS NOT NULL AND [OccurrenceLocalDate] IS NOT NULL")
            .HasDatabaseName("UX_Sessions_ScheduleRule_Occurrence");

        builder.HasOne<AcademicClass>()
            .WithMany()
            .HasForeignKey(session => new { session.OrganizationId, session.ClassId })
            .HasPrincipalKey(academicClass => new { academicClass.OrganizationId, academicClass.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleRule>()
            .WithMany()
            .HasForeignKey(session => new
            {
                session.OrganizationId,
                session.ClassId,
                session.ScheduleRuleId
            })
            .HasPrincipalKey(rule => new { rule.OrganizationId, rule.ClassId, rule.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
