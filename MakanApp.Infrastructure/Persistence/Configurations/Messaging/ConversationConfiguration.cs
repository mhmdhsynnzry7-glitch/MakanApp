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
            table.HasCheckConstraint("CK_Conversations_Type", "[Type] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Conversations_Scope", "[Scope] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_Status", "[Status] IN (1, 2)");
            table.HasCheckConstraint("CK_Conversations_ManagementPolicy", "[ManagementPolicy] IN (0, 1, 2)");
            table.HasCheckConstraint(
                "CK_Conversations_ScopeOrganization",
                "([Scope] = 1 AND [OrganizationId] IS NULL) OR ([Scope] = 2 AND [OrganizationId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_DirectPair",
                "[Type] <> 1 OR ([DirectUserLowId] IS NOT NULL AND [DirectUserHighId] IS NOT NULL AND [DirectUserLowId] <> [DirectUserHighId])");
            table.HasCheckConstraint(
                "CK_Conversations_ManagedShape",
                "([Type] = 1 AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] IN (2, 3) AND [ManagementPolicy] IN (1, 2) AND LEN(LTRIM(RTRIM([Title]))) > 0 AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_UserManagedCreation",
                "([ManagementPolicy] <> 1) OR ([CreatedByUserId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Conversations_SystemManagedAcademic",
                "([ManagementPolicy] <> 2) OR ([Scope] = 2 AND [CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationPayloadHash] IS NULL)");
            table.HasCheckConstraint("CK_Conversations_NextMessageSequence", "[NextMessageSequence] > 0");
        });
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Title)
            .HasMaxLength(Conversation.TitleMaximumLength);
        builder.Property(conversation => conversation.Description)
            .HasMaxLength(Conversation.DescriptionMaximumLength);
        builder.Property(conversation => conversation.CreationPayloadHash)
            .HasMaxLength(Conversation.CreationPayloadHashLength)
            .IsFixedLength();
        builder.Property(conversation => conversation.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(conversation => conversation.ArchivedAtUtc).HasColumnType("datetime2(7)");
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
        builder.HasIndex(conversation => new
        {
            conversation.CreatedByUserId,
            conversation.ClientOperationId
        })
            .IsUnique()
            .HasFilter("[ManagementPolicy] = 1")
            .HasDatabaseName("UX_Conversations_Creator_ClientOperationId");

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
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
