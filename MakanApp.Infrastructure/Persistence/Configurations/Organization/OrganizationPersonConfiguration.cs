using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Organization;

public sealed class OrganizationPersonConfiguration : IEntityTypeConfiguration<OrganizationPerson>
{
    public void Configure(EntityTypeBuilder<OrganizationPerson> builder)
    {
        builder.ToTable("OrganizationPersons", "organization", table =>
            table.HasCheckConstraint(
                "CK_OrganizationPersons_Status",
                "[Status] IN (1, 2)"));
        builder.HasKey(organizationPerson => organizationPerson.Id);
        builder.HasAlternateKey(organizationPerson => new
        {
            organizationPerson.OrganizationId,
            organizationPerson.Id
        })
            .HasName("UQ_OrganizationPersons_OrganizationId_Id");
        builder.Property(organizationPerson => organizationPerson.CreatedAtUtc)
            .HasColumnType("datetime2(7)");
        builder.Property(organizationPerson => organizationPerson.ActivatedAtUtc)
            .HasColumnType("datetime2(7)");
        builder.Property(organizationPerson => organizationPerson.EndedAtUtc)
            .HasColumnType("datetime2(7)");
        builder.Property(organizationPerson => organizationPerson.RowVersion).IsRowVersion();

        builder.HasIndex(organizationPerson => new
        {
            organizationPerson.OrganizationId,
            organizationPerson.PersonId
        })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_OrganizationPersons_Active_Organization_Person");

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(organizationPerson => organizationPerson.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(organizationPerson => organizationPerson.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
