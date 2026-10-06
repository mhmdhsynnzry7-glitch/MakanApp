using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<MessagingChangeDatabaseRecord[]> GetMessagingChangesAsync(Guid conversationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.MessagingChangeEvents
            .AsNoTracking()
            .Where(change => change.ConversationId == conversationId)
            .OrderBy(change => change.ChangeSequence)
            .Select(change => new MessagingChangeDatabaseRecord(
                change.Id,
                change.ChangeSequence,
                change.ChangeType,
                change.ResourceId,
                change.AudienceUserId))
            .ToArrayAsync();
    }

    public async Task<ParticipantCursorDatabaseRecord> GetParticipantCursorAsync(
        Guid conversationId,
        Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.ConversationId == conversationId &&
                                  participant.UserId == userId &&
                                  participant.Status == ConversationParticipantStatus.Active)
            .Select(participant => new ParticipantCursorDatabaseRecord(
                participant.LastDeliveredMessageSequence,
                participant.LastReadMessageSequence,
                participant.CursorUpdatedAtUtc))
            .SingleAsync();
    }

    public async Task SeedMessagesForCursorConcurrencyAsync(
        Guid conversationId,
        Guid senderUserId,
        int count)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var conversation = await dbContext.Conversations.SingleAsync(item => item.Id == conversationId);
        var participant = await dbContext.ConversationParticipants.SingleAsync(item =>
            item.ConversationId == conversationId &&
            item.UserId == senderUserId &&
            item.Status == ConversationParticipantStatus.Active);
        var start = DateTime.UtcNow;
        for (var index = 0; index < count; index++)
        {
            var createdAtUtc = start.AddTicks(index);
            var message = Message.CreateText(
                conversationId,
                participant.Id,
                senderUserId,
                Guid.NewGuid(),
                conversation.AllocateNextMessageSequence(),
                $"seed-{index + 1}",
                Message.StorageMaximumTextLength,
                createdAtUtc);
            dbContext.Messages.Add(message);
            dbContext.MessageRevisions.Add(MessageRevision.Create(
                message.Id,
                1,
                message.Text,
                senderUserId,
                createdAtUtc));
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountPendingRealtimeOutboxAsync(Guid conversationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await (
            from outbox in dbContext.MessagingRealtimeOutboxMessages.AsNoTracking()
            join change in dbContext.MessagingChangeEvents.AsNoTracking()
                on outbox.ChangeEventId equals change.Id
            where change.ConversationId == conversationId && outbox.DispatchedAtUtc == null
            select outbox.Id)
            .CountAsync();
    }
}

public sealed record MessagingChangeDatabaseRecord(
    Guid Id,
    long ChangeSequence,
    MessagingChangeType ChangeType,
    Guid ResourceId,
    Guid? AudienceUserId);

public sealed record ParticipantCursorDatabaseRecord(
    long LastDeliveredMessageSequence,
    long LastReadMessageSequence,
    DateTime? CursorUpdatedAtUtc);
