using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingSyncModelTests
{
    [Fact]
    public void MessageAndChangeSequencesAdvanceIndependently()
    {
        var conversation = Conversation.CreateDirect(
            ConversationScope.Personal,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(1, conversation.AllocateNextMessageSequence());
        Assert.Equal(1, conversation.AllocateNextChangeSequence());
        Assert.Equal(2, conversation.AllocateNextChangeSequence());
        Assert.Equal(2, conversation.NextMessageSequence);
        Assert.Equal(3, conversation.NextChangeSequence);
    }

    [Fact]
    public void DeliveryAndReadCursorsNeverRegress()
    {
        var participant = ConversationParticipant.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.True(participant.AdvanceDelivery(50, DateTime.UtcNow));
        Assert.False(participant.AdvanceDelivery(40, DateTime.UtcNow));
        Assert.True(participant.AdvanceRead(30, DateTime.UtcNow));
        Assert.False(participant.AdvanceRead(20, DateTime.UtcNow));
        Assert.Equal(50, participant.LastDeliveredMessageSequence);
        Assert.Equal(30, participant.LastReadMessageSequence);
    }

    [Fact]
    public void ReadCursorAlsoAdvancesDeliveryCursor()
    {
        var participant = ConversationParticipant.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.True(participant.AdvanceRead(17, DateTime.UtcNow));
        Assert.Equal(17, participant.LastReadMessageSequence);
        Assert.Equal(17, participant.LastDeliveredMessageSequence);
    }

    [Fact]
    public void ChangeEventCarriesMinimalStableMetadata()
    {
        var conversationId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var change = MessagingChangeEvent.Create(
            conversationId,
            7,
            MessagingChangeType.MessageDeleted,
            resourceId,
            "version-2",
            DateTime.UtcNow,
            actorId);

        Assert.Equal(conversationId, change.ConversationId);
        Assert.Equal(7, change.ChangeSequence);
        Assert.Equal(MessagingChangeType.MessageDeleted, change.ChangeType);
        Assert.Equal(resourceId, change.ResourceId);
        Assert.Equal("version-2", change.ResourceVersion);
        Assert.Equal(MessagingChangeEvent.CurrentPayloadVersion, change.PayloadVersion);
    }

    [Fact]
    public void ChangeCursorIsConversationScopedAndOpaque()
    {
        var conversationId = Guid.NewGuid();
        var cursor = MessagingChangeCursor.Encode(conversationId, 42);

        Assert.True(MessagingChangeCursor.TryDecode(cursor, conversationId, out var sequence));
        Assert.Equal(42, sequence);
        Assert.False(MessagingChangeCursor.TryDecode(cursor, Guid.NewGuid(), out _));
        Assert.False(MessagingChangeCursor.TryDecode("invalid", conversationId, out _));
    }
}
