using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

public sealed class EfFileAssetBoundAccessResolver(MakanDbContext dbContext)
    : IFileAssetBoundAccessResolver
{
    public Task<bool> CanReadBoundFileAsync(
        FileAsset fileAsset,
        AccessContext accessContext,
        CancellationToken cancellationToken) =>
        (
            from attachment in dbContext.MessageAttachments
            join message in dbContext.Messages on attachment.MessageId equals message.Id
            join conversation in dbContext.Conversations on message.ConversationId equals conversation.Id
            join participant in dbContext.ConversationParticipants
                on conversation.Id equals participant.ConversationId
            where attachment.FileAssetId == fileAsset.Id &&
                  message.DeletedAtUtc == null &&
                  participant.UserId == accessContext.UserId &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  (conversation.Scope == ConversationScope.Personal &&
                   accessContext.WorkspaceType == WorkspaceType.Personal &&
                   accessContext.OrganizationId == null ||
                   conversation.Scope == ConversationScope.Organization &&
                   accessContext.WorkspaceType == WorkspaceType.Organization &&
                   conversation.OrganizationId == accessContext.OrganizationId &&
                   accessContext.MembershipId != null &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == accessContext.MembershipId &&
                       membership.UserId == accessContext.UserId &&
                       membership.OrganizationId == conversation.OrganizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null))
            select attachment)
            .AnyAsync(cancellationToken);
}
