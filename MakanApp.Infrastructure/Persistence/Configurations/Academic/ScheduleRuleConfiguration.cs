using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class ScheduleRuleConfiguration : IEntityTypeConfiguration<ScheduleRule>
{
    public void Configure(EntityTypeBuilder<ScheduleRule> builder)
    {
        builder.ToTable("ScheduleRules", "academic", table =>
        {
            table.HasCheckConstraint("CK_ScheduleRules_LocalDayOfWeek", "[LocalDayOfWeek] BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_ScheduleRules_Duration", "[DurationMinutes] > 0 AND [DurationMinutes] <= 1440");
            table.HasCheckConstraint("CK_ScheduleRules_DateRange", "[EffectiveUntil] >= [EffectiveFrom]");
            table.HasCheckConstraint("CK_ScheduleRules_Status", "[Status] IN (1, 2)");
        });
        builder.HasKey(rule => rule.Id);
        builder.HasAlternateKey(rule => new
        {
            rule.OrganizationId,
            rule.ClassId,
            rule.Id
        }).HasName("UQ_ScheduleRules_OrganizationId_ClassId_Id");
        builder.Property(rule => rule.LocalStartTime).HasColumnType("time(0)");
        builder.Property(rule => rule.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(rule => rule.SessionTitle).HasMaxLength(200).IsRequired();
        builder.Property(rule => rule.MeetingUrl).HasMaxLength(1000);
        builder.Property(rule => rule.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(rule => rule.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(rule => rule.RowVersion).IsRowVersion();

        builder.HasOne<AcademicClass>()
            .WithMany()
            .HasForeignKey(rule => new { rule.OrganizationId, rule.ClassId })
            .HasPrincipalKey(academicClass => new { academicClass.OrganizationId, academicClass.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
