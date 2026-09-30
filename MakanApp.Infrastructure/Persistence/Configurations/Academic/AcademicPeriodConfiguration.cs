using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Academic;

public sealed class AcademicPeriodConfiguration : IEntityTypeConfiguration<AcademicPeriod>
{
    public void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        builder.ToTable("AcademicPeriods", "academic", table =>
        {
            table.HasCheckConstraint("CK_AcademicPeriods_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_AcademicPeriods_DateRange", "[EndDate] > [StartDate]");
        });
        builder.HasKey(period => period.Id);
        builder.HasAlternateKey(period => new { period.OrganizationId, period.Id })
            .HasName("UQ_AcademicPeriods_OrganizationId_Id");
        builder.Property(period => period.Title).HasMaxLength(200).IsRequired();
        builder.Property(period => period.StartDate).HasColumnType("date");
        builder.Property(period => period.EndDate).HasColumnType("date");
        builder.Property(period => period.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(period => period.RowVersion).IsRowVersion();

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(period => period.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
