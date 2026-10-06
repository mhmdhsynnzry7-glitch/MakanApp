using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public int MaximumTextLength { get; init; } = Message.StorageMaximumTextLength;
    public int DefaultHistoryLimit { get; init; } = 50;
    public int MaximumHistoryLimit { get; init; } = 100;
    public int MaximumAttachmentsPerMessage { get; init; } = 10;
    public int MaximumMentionsPerMessage { get; init; } = 50;
    public int DefaultChangeLimit { get; init; } = 100;
    public int MaximumChangeLimit { get; init; } = 200;
}
