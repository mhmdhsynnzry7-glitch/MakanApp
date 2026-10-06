using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<int> CountMessageRevisionsAsync(Guid messageId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.MessageRevisions.CountAsync(
            revision => revision.MessageId == messageId);
    }

    public async Task<string?[]> GetMessageRevisionTextsAsync(Guid messageId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.MessageRevisions
            .AsNoTracking()
            .Where(revision => revision.MessageId == messageId)
            .OrderBy(revision => revision.RevisionNumber)
            .Select(revision => revision.Text)
            .ToArrayAsync();
    }

    public async Task<int> CountActiveMessageReactionsAsync(Guid messageId, Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.MessageReactions.CountAsync(
            reaction => reaction.MessageId == messageId &&
                        reaction.UserId == userId &&
                        reaction.RemovedAtUtc == null);
    }

    public async Task<int> CountActiveConversationPinsAsync(Guid conversationId, Guid messageId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.ConversationPins.CountAsync(
            pin => pin.ConversationId == conversationId &&
                   pin.MessageId == messageId &&
                   pin.UnpinnedAtUtc == null);
    }

    public async Task<int> CountMessageAttachmentsAsync(Guid messageId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.MessageAttachments.CountAsync(
            attachment => attachment.MessageId == messageId);
    }

    public async Task<bool> IsFileAssetRetainedAsync(Guid fileAssetId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.FileAssets.AnyAsync(
            file => file.Id == fileAssetId && file.RetainedAtUtc != null);
    }

    public async Task<MessageDatabaseAdvancedRecord> GetAdvancedMessageAsync(Guid messageId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.Id == messageId)
            .Select(message => new MessageDatabaseAdvancedRecord(
                message.Id,
                message.ConversationId,
                message.Sequence,
                message.Kind,
                message.Text,
                message.CurrentRevisionNumber,
                message.EditedAtUtc,
                message.DeletedAtUtc,
                message.ForwardedFromMessageId))
            .SingleAsync();
    }
}

public sealed record MessageDatabaseAdvancedRecord(
    Guid Id,
    Guid ConversationId,
    long Sequence,
    MessageKind Kind,
    string? Text,
    int CurrentRevisionNumber,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    Guid? ForwardedFromMessageId);
