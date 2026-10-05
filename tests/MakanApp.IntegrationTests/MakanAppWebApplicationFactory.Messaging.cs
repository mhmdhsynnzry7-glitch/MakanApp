using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task SetCommunicationAgeCategoryAsync(
        Guid userId,
        CommunicationAgeCategory category)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
        user.SetCommunicationAgeCategory(category, DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Guid> CreatePersonalCommunicationGrantAsync(
        Guid firstUserId,
        Guid secondUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var grant = PersonalCommunicationGrant.CreateActive(
            firstUserId,
            secondUserId,
            DateTime.UtcNow);
        dbContext.PersonalCommunicationGrants.Add(grant);
        await dbContext.SaveChangesAsync();
        return grant.Id;
    }

    public async Task RevokePersonalCommunicationGrantAsync(Guid grantId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var grant = await dbContext.PersonalCommunicationGrants.SingleAsync(item => item.Id == grantId);
        grant.Revoke(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountDirectConversationsAsync(
        Guid firstUserId,
        Guid secondUserId,
        ConversationScope scope,
        Guid? organizationId)
    {
        var pair = DirectUserPair.Create(firstUserId, secondUserId);
        await using var serviceScope = Services.CreateAsyncScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Conversations.CountAsync(conversation =>
            conversation.Type == ConversationType.Direct &&
            conversation.Scope == scope &&
            conversation.OrganizationId == organizationId &&
            conversation.DirectUserLowId == pair.LowerUserId &&
            conversation.DirectUserHighId == pair.HigherUserId);
    }

    public async Task<Guid[]> GetConversationParticipantUserIdsAsync(Guid conversationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.ConversationId == conversationId)
            .OrderBy(participant => participant.UserId)
            .Select(participant => participant.UserId)
            .ToArrayAsync();
    }

    public async Task<MessageDatabaseRecord[]> GetMessagesAsync(Guid conversationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.Sequence)
            .Select(message => new MessageDatabaseRecord(
                message.Id,
                message.SenderUserId,
                message.ClientMessageId,
                message.Sequence,
                message.Text,
                message.SentAtUtc))
            .ToArrayAsync();
    }

    public async Task<ConversationParticipantDatabaseRecord[]> GetConversationParticipantsAsync(
        Guid conversationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.ConversationId == conversationId)
            .OrderBy(participant => participant.JoinedAtUtc)
            .Select(participant => new ConversationParticipantDatabaseRecord(
                participant.Id,
                participant.UserId,
                participant.Role,
                participant.Status,
                participant.EndedAtUtc))
            .ToArrayAsync();
    }

    public async Task<int> CountManagedConversationsAsync(
        Guid creatorUserId,
        Guid clientOperationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Conversations.CountAsync(conversation =>
            conversation.CreatedByUserId == creatorUserId &&
            conversation.ClientOperationId == clientOperationId &&
            conversation.ManagementPolicy == ConversationManagementPolicy.UserManaged);
    }

    public async Task<Guid> CreateSystemManagedConversationAsync(
        Guid organizationId,
        Guid participantUserId,
        ConversationType type)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var conversation = Conversation.CreateSystemManagedAcademic(
            type,
            organizationId,
            "گفتگوی کلاسی مدیریت‌شده",
            null,
            DateTime.UtcNow);
        dbContext.Conversations.Add(conversation);
        dbContext.ConversationParticipants.Add(ConversationParticipant.CreateActive(
            conversation.Id,
            participantUserId,
            DateTime.UtcNow,
            ConversationParticipantRole.Member));
        await dbContext.SaveChangesAsync();
        return conversation.Id;
    }
}

public sealed record ConversationParticipantDatabaseRecord(
    Guid Id,
    Guid UserId,
    ConversationParticipantRole Role,
    ConversationParticipantStatus Status,
    DateTime? EndedAtUtc);

public sealed record MessageDatabaseRecord(
    Guid Id,
    Guid SenderUserId,
    Guid ClientMessageId,
    long Sequence,
    string Text,
    DateTime SentAtUtc);
