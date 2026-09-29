using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<MakanApp.Domain.Organization.Organization>
{
    public void Configure(EntityTypeBuilder<MakanApp.Domain.Organization.Organization> builder)
    {
        builder.ToTable("Organizations", "organization", table =>
            table.HasCheckConstraint(
                "CK_Organizations_Status",
                "[Status] IN (1, 2)"));
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
        builder.Property(organization => organization.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(organization => organization.RowVersion).IsRowVersion();
    }
}
