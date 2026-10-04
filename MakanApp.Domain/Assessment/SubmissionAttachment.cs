namespace MakanApp.Domain.Assessment;

public sealed class SubmissionAttachment
{
    private SubmissionAttachment()
    {
    }

    private SubmissionAttachment(
        Guid id,
        Guid organizationId,
        Guid submissionAttemptId,
        Guid fileAssetId,
        DateTime attachedAtUtc)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty ||
            submissionAttemptId == Guid.Empty || fileAssetId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های پیوست ارسال الزامی هستند.");
        }

        if (attachedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان اتصال فایل باید UTC باشد.", nameof(attachedAtUtc));
        }

        Id = id;
        OrganizationId = organizationId;
        SubmissionAttemptId = submissionAttemptId;
        FileAssetId = fileAssetId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid SubmissionAttemptId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public DateTime AttachedAtUtc { get; private set; }

    public static SubmissionAttachment Create(
        Guid organizationId,
        Guid submissionAttemptId,
        Guid fileAssetId,
        DateTime attachedAtUtc) =>
        new(Guid.NewGuid(), organizationId, submissionAttemptId, fileAssetId, attachedAtUtc);
}
