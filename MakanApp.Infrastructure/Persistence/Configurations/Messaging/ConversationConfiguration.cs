using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationEntity = MakanApp.Domain.Organization.Organization;

namespace MakanApp.Infrastructure.Persistence.Configurations.Messaging;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations", "messaging", table =>
        {
            table.HasCheckConstraint("CK_Conversations_Type", "[Type] IN (1)");
            table.HasCheckConstraint("CK_Conversations_Scope", "[Scope] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint(
                "CK_Conversations_ScopeOrganization",
                "([Scope] = 1 AND [OrganizationId] IS NULL) OR ([Scope] = 2 AND [OrganizationId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_DirectPair",
                "[Type] <> 1 OR ([DirectUserLowId] IS NOT NULL AND [DirectUserHighId] IS NOT NULL AND [DirectUserLowId] <> [DirectUserHighId])");
            table.HasCheckConstraint("CK_Conversations_NextMessageSequence", "[NextMessageSequence] > 0");
        });
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(conversation => conversation.RowVersion).IsRowVersion();

        builder.HasIndex(conversation => new
        {
            conversation.Scope,
            conversation.OrganizationId,
            conversation.DirectUserLowId,
            conversation.DirectUserHighId
        })
            .IsUnique()
            .HasFilter("[Type] = 1")
            .HasDatabaseName("UX_Conversations_Direct_Scope_Organization_Pair");

        builder.HasOne<OrganizationEntity>()
            .WithMany()
            .HasForeignKey(conversation => conversation.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.DirectUserLowId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.DirectUserHighId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
