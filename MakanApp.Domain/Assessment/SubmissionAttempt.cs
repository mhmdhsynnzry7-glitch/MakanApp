namespace MakanApp.Domain.Assessment;

public sealed class SubmissionAttempt
{
    public const int MaximumAnswerLength = 50_000;

    private SubmissionAttempt()
    {
    }

    private SubmissionAttempt(
        Guid id,
        Guid organizationId,
        Guid assignmentId,
        Guid assignmentVersionId,
        Guid assignmentRecipientId,
        Guid enrollmentId,
        int attemptNumber,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || assignmentId == Guid.Empty ||
            assignmentVersionId == Guid.Empty || assignmentRecipientId == Guid.Empty ||
            enrollmentId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های تلاش ارسال الزامی هستند.");
        }

        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان ایجاد تلاش باید UTC باشد.", nameof(createdAtUtc));
        }

        Id = id;
        OrganizationId = organizationId;
        AssignmentId = assignmentId;
        AssignmentVersionId = assignmentVersionId;
        AssignmentRecipientId = assignmentRecipientId;
        EnrollmentId = enrollmentId;
        AttemptNumber = attemptNumber;
        Status = SubmissionAttemptStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid AssignmentVersionId { get; private set; }
    public Guid AssignmentRecipientId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public int AttemptNumber { get; private set; }
    public SubmissionAttemptStatus Status { get; private set; }
    public string? AnswerText { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastSavedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public bool IsLate { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == SubmissionAttemptStatus.Draft;

    public static SubmissionAttempt CreateDraft(
        Guid organizationId,
        Guid assignmentId,
        Guid assignmentVersionId,
        Guid assignmentRecipientId,
        Guid enrollmentId,
        int attemptNumber,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            assignmentId,
            assignmentVersionId,
            assignmentRecipientId,
            enrollmentId,
            attemptNumber,
            createdAtUtc);

    public void SaveAnswer(string? answerText, DateTime savedAtUtc)
    {
        EnsureDraft();
        AnswerText = NormalizeAnswer(answerText);
        LastSavedAtUtc = EnsureUtc(savedAtUtc, nameof(savedAtUtc));
    }

    public void MarkAttachmentsChanged(DateTime changedAtUtc)
    {
        EnsureDraft();
        LastSavedAtUtc = EnsureUtc(changedAtUtc, nameof(changedAtUtc));
    }

    public void Submit(
        DateTime submittedAtUtc,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        bool hasReadyAttachment)
    {
        EnsureDraft();
        submittedAtUtc = EnsureUtc(submittedAtUtc, nameof(submittedAtUtc));
        dueAtUtc = EnsureUtc(dueAtUtc, nameof(dueAtUtc));

        var isLate = submittedAtUtc > dueAtUtc;
        if (isLate && !allowLateSubmission)
        {
            throw new SubmissionDeadlinePassedException();
        }

        if (string.IsNullOrWhiteSpace(AnswerText) && !hasReadyAttachment)
        {
            throw new SubmissionEmptyException();
        }

        Status = SubmissionAttemptStatus.Submitted;
        SubmittedAtUtc = submittedAtUtc;
        IsLate = isLate;
        LastSavedAtUtc ??= submittedAtUtc;
    }

    private static string? NormalizeAnswer(string? answerText)
    {
        var normalized = answerText?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > MaximumAnswerLength)
        {
            throw new ArgumentException("متن پاسخ از طول مجاز بیشتر است.", nameof(answerText));
        }

        return normalized;
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("تلاش ارسال‌شده تغییرپذیر نیست.");
        }
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            // SQL Server datetime2 اطلاعات Kind را نگه نمی‌دارد؛ مقدار persist‌شده همچنان UTC است.
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentException("زمان باید UTC باشد.", parameterName)
        };
}

public sealed class SubmissionDeadlinePassedException : Exception
{
}

public sealed class SubmissionEmptyException : Exception
{
}
