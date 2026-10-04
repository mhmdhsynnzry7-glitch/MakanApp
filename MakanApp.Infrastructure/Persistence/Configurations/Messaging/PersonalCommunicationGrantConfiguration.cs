using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class PersonalCommunicationGrantConfiguration :
    IEntityTypeConfiguration<PersonalCommunicationGrant>
{
    public void Configure(EntityTypeBuilder<PersonalCommunicationGrant> builder)
    {
        builder.ToTable("PersonalCommunicationGrants", "messaging", table =>
        {
            table.HasCheckConstraint("CK_PersonalCommunicationGrants_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_PersonalCommunicationGrants_DistinctUsers",
                "[LowerUserId] <> [HigherUserId]");
        });
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.GrantedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(grant => grant.EndedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(grant => grant.RowVersion).IsRowVersion();

        builder.HasIndex(grant => new { grant.LowerUserId, grant.HigherUserId })
            .IsUnique()
            .HasDatabaseName("UX_PersonalCommunicationGrants_UserPair");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(grant => grant.LowerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(grant => grant.HigherUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
