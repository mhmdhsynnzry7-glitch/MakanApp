using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class AdvancedMessagingModelTests
{
    [Theory]
    [InlineData(MessageKind.Image)]
    [InlineData(MessageKind.Video)]
    [InlineData(MessageKind.Voice)]
    [InlineData(MessageKind.File)]
    public void MediaMessageRequiresAttachment(MessageKind kind)
    {
        Assert.Throws<ArgumentException>(() => CreateMessage(kind, null, 0));
    }

    [Fact]
    public void VoiceIsStoredMediaMessageAndDoesNotModelACall()
    {
        var message = CreateMessage(MessageKind.Voice, null, 1);

        Assert.Equal(MessageKind.Voice, message.Kind);
        Assert.Null(message.Text);
    }

    [Fact]
    public void EditPreservesIdentityAndSequenceWhileAdvancingRevision()
    {
        var message = CreateMessage(MessageKind.Text, "متن اول", 0);
        var id = message.Id;
        var sequence = message.Sequence;

        var revision = message.EditText("متن دوم", 4000, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(id, message.Id);
        Assert.Equal(sequence, message.Sequence);
        Assert.Equal(2, revision);
        Assert.True(message.IsEdited);
        Assert.Equal("متن دوم", message.Text);
    }

    [Fact]
    public void DeleteCreatesTombstoneAndPreventsNormalEdit()
    {
        var message = CreateMessage(MessageKind.Text, "متن محرمانه", 0);

        message.Delete(message.SenderUserId, DateTime.UtcNow.AddMinutes(1));

        Assert.True(message.IsDeleted);
        Assert.Null(message.Text);
        Assert.Throws<InvalidOperationException>(() =>
            message.EditText("بازگردانی", 4000, DateTime.UtcNow.AddMinutes(2)));
    }

    [Fact]
    public void RevisionStoresExplicitHistoricalContent()
    {
        var messageId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var revision = MessageRevision.Create(
            messageId,
            1,
            "متن تاریخی",
            authorId,
            DateTime.UtcNow);

        Assert.Equal(messageId, revision.MessageId);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal("متن تاریخی", revision.Text);
    }

    [Fact]
    public void ReactionRemovalPreservesTombstoneLifecycle()
    {
        var reaction = MessageReaction.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageReactionType.Love,
            DateTime.UtcNow);

        reaction.Remove(DateTime.UtcNow.AddMinutes(1));

        Assert.False(reaction.IsActive);
        Assert.NotNull(reaction.RemovedAtUtc);
    }

    [Fact]
    public void PinRemovalPreservesWhoAndWhen()
    {
        var actorId = Guid.NewGuid();
        var pin = ConversationPin.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            actorId,
            DateTime.UtcNow);

        pin.Unpin(actorId, DateTime.UtcNow.AddMinutes(1));

        Assert.False(pin.IsActive);
        Assert.Equal(actorId, pin.UnpinnedByUserId);
    }

    private static Message CreateMessage(MessageKind kind, string? text, int attachmentCount) =>
        Message.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            7,
            kind,
            text,
            attachmentCount,
            null,
            null,
            4000,
            DateTime.UtcNow);
}
