using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Guardian;

public sealed class GuardianRelationConfiguration : IEntityTypeConfiguration<GuardianRelation>
{
    public void Configure(EntityTypeBuilder<GuardianRelation> builder)
    {
        builder.ToTable("GuardianRelations", "guardian", table =>
            table.HasCheckConstraint(
                "CK_GuardianRelations_Status",
                "[Status] IN (1, 2, 3, 4)"));
        builder.HasKey(relation => relation.Id);
        builder.Property(relation => relation.ValidFromUtc).HasColumnType("datetime2(7)");
        builder.Property(relation => relation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(relation => relation.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(relation => relation.RowVersion).IsRowVersion();

        builder.HasIndex(relation => new
        {
            relation.OrganizationId,
            relation.GuardianUserId,
            relation.LearnerOrganizationPersonId
        })
            .IsUnique()
            .HasFilter("[Status] = 2 AND [EndedAtUtc] IS NULL")
            .HasDatabaseName("UX_GuardianRelations_Active_Organization_Guardian_Learner");

        builder.HasOne<MakanApp.Domain.Organization.Organization>()
            .WithMany()
            .HasForeignKey(relation => relation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(relation => relation.GuardianUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationPerson>()
            .WithMany()
            .HasForeignKey(relation => new
            {
                relation.OrganizationId,
                relation.LearnerOrganizationPersonId
            })
            .HasPrincipalKey(organizationPerson => new
            {
                organizationPerson.OrganizationId,
                organizationPerson.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
