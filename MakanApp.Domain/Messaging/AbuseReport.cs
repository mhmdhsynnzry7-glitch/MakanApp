namespace MakanApp.Domain.Messaging;

public sealed class AbuseReport
{
    public const int MaximumDescriptionLength = 1000;

    private AbuseReport()
    {
    }

    private AbuseReport(
        Guid id,
        Guid reporterUserId,
        Guid reportedUserId,
        Guid conversationId,
        Guid messageId,
        Guid clientReportId,
        AbuseReportReason reason,
        string? description,
        MessageKind reportedMessageKind,
        string? reportedContentSnapshot,
        byte[] reportedMessageVersion,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        ReportedUserId = reportedUserId;
        ConversationId = conversationId;
        MessageId = messageId;
        ClientReportId = clientReportId;
        Reason = reason;
        Description = description;
        Status = AbuseReportStatus.Submitted;
        ReportedMessageKind = reportedMessageKind;
        ReportedContentSnapshot = reportedContentSnapshot;
        ReportedMessageVersion = reportedMessageVersion;
        RequestPayloadHash = requestPayloadHash;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public Guid ReportedUserId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid ClientReportId { get; private set; }
    public AbuseReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public AbuseReportStatus Status { get; private set; }
    public MessageKind ReportedMessageKind { get; private set; }
    public string? ReportedContentSnapshot { get; private set; }
    public byte[] ReportedMessageVersion { get; private set; } = [];
    public byte[] RequestPayloadHash { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AbuseReport Create(
        Guid reporterUserId,
        Guid reportedUserId,
        Guid conversationId,
        Guid messageId,
        Guid clientReportId,
        AbuseReportReason reason,
        string? description,
        MessageKind reportedMessageKind,
        string? reportedContentSnapshot,
        byte[] reportedMessageVersion,
        byte[] requestPayloadHash,
        DateTime createdAtUtc)
    {
        if (reporterUserId == Guid.Empty || reportedUserId == Guid.Empty ||
            conversationId == Guid.Empty || messageId == Guid.Empty || clientReportId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های گزارش و پیام الزامی هستند.");
        }

        if (!Enum.IsDefined(reason) || !Enum.IsDefined(reportedMessageKind))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        if (normalizedDescription?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException("توضیح گزارش از طول مجاز بیشتر است.", nameof(description));
        }

        if (reportedContentSnapshot?.Length > Message.StorageMaximumTextLength)
        {
            throw new ArgumentException("محتوای شاهد از طول مجاز بیشتر است.", nameof(reportedContentSnapshot));
        }

        if (reportedMessageVersion.Length != 8 || requestPayloadHash.Length != 32)
        {
            throw new ArgumentException("نسخه پیام یا اثر انگشت درخواست معتبر نیست.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان ثبت گزارش باید UTC باشد.", nameof(createdAtUtc));
        }

        return new AbuseReport(
            Guid.NewGuid(),
            reporterUserId,
            reportedUserId,
            conversationId,
            messageId,
            clientReportId,
            reason,
            normalizedDescription,
            reportedMessageKind,
            reportedContentSnapshot,
            reportedMessageVersion.ToArray(),
            requestPayloadHash.ToArray(),
            createdAtUtc);
    }
}
