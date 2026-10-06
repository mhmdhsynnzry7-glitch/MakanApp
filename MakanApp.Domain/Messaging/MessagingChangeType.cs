namespace MakanApp.Domain.Messaging;

public enum MessagingChangeType
{
    MessageCreated = 1,
    MessageEdited = 2,
    MessageDeleted = 3,
    ReactionChanged = 4,
    PinChanged = 5,
    ParticipantChanged = 6,
    ConversationChanged = 7,
    ReadCursorAdvanced = 8,
    DeliveryCursorAdvanced = 9
}
