namespace MakanApp.Application.Messaging;

public static class MessagingErrorCodes
{
    public const string ConversationNotFound = "CONVERSATION_NOT_FOUND";
    public const string ConversationNotAllowed = "CONVERSATION_NOT_ALLOWED";
    public const string DirectRecipientNotAvailable = "DIRECT_RECIPIENT_NOT_AVAILABLE";
    public const string DirectConversationConflict = "DIRECT_CONVERSATION_CONFLICT";
    public const string MessageEmpty = "MESSAGE_EMPTY";
    public const string MessageTooLong = "MESSAGE_TOO_LONG";
    public const string MessageNotAllowed = "MESSAGE_NOT_ALLOWED";
    public const string MessageClientIdRequired = "MESSAGE_CLIENT_ID_REQUIRED";
    public const string MessageIdempotencyConflict = "MESSAGE_IDEMPOTENCY_CONFLICT";
    public const string MessagePageInvalid = "MESSAGE_PAGE_INVALID";
    public const string ParticipantNotActive = "PARTICIPANT_NOT_ACTIVE";
    public const string OrganizationScopeMismatch = "ORGANIZATION_SCOPE_MISMATCH";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
