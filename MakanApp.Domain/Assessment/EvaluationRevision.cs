namespace MakanApp.Domain.Assessment;

public sealed class EvaluationRevision
{
    public const int MaximumFeedbackLength = 10_000;
    public const int MaximumPrivateNoteLength = 10_000;
    public const int MaximumCorrectionReasonLength = 1_000;

    private EvaluationRevision()
    {
    }

    private EvaluationRevision(
        Guid id,
        Guid organizationId,
        Guid submissionAttemptId,
        int revisionNumber,
        decimal score,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? teacherPrivateNote,
        Guid createdByMembershipId,
        DateTime createdAtUtc,
        Guid? supersedesEvaluationRevisionId,
        string? correctionReason)
    {
        Id = id;
        OrganizationId = organizationId;
        SubmissionAttemptId = submissionAttemptId;
        RevisionNumber = revisionNumber;
        Score = score;
        LearnerFeedback = learnerFeedback;
        GuardianVisibleFeedback = guardianVisibleFeedback;
        TeacherPrivateNote = teacherPrivateNote;
        Status = EvaluationRevisionStatus.Draft;
        CreatedByMembershipId = createdByMembershipId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        SupersedesEvaluationRevisionId = supersedesEvaluationRevisionId;
        CorrectionReason = correctionReason;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid SubmissionAttemptId { get; private set; }
    public int RevisionNumber { get; private set; }
    public decimal Score { get; private set; }
    public string? LearnerFeedback { get; private set; }
    public string? GuardianVisibleFeedback { get; private set; }
    public string? TeacherPrivateNote { get; private set; }
    public EvaluationRevisionStatus Status { get; private set; }
    public Guid CreatedByMembershipId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? SupersedesEvaluationRevisionId { get; private set; }
    public string? CorrectionReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == EvaluationRevisionStatus.Draft;
    public bool IsReleased => Status == EvaluationRevisionStatus.Released;

    public static EvaluationRevision CreateDraft(
        Guid organizationId,
        Guid submissionAttemptId,
        int revisionNumber,
        decimal score,
        decimal maxScore,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? teacherPrivateNote,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        ValidateIdentity(organizationId, submissionAttemptId, createdByMembershipId, revisionNumber);
        ValidateValues(
            score,
            maxScore,
            learnerFeedback,
            guardianVisibleFeedback,
            teacherPrivateNote,
            createdAtUtc);

        return new EvaluationRevision(
            Guid.NewGuid(),
            organizationId,
            submissionAttemptId,
            revisionNumber,
            score,
            Normalize(learnerFeedback),
            Normalize(guardianVisibleFeedback),
            Normalize(teacherPrivateNote),
            createdByMembershipId,
            createdAtUtc,
            null,
            null);
    }

    public static EvaluationRevision CreateCorrection(
        Guid organizationId,
        Guid submissionAttemptId,
        int revisionNumber,
        decimal score,
        decimal maxScore,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? teacherPrivateNote,
        Guid createdByMembershipId,
        DateTime createdAtUtc,
        Guid supersedesEvaluationRevisionId,
        string correctionReason)
    {
        ValidateIdentity(organizationId, submissionAttemptId, createdByMembershipId, revisionNumber);
        if (supersedesEvaluationRevisionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه ارزیابی قبلی الزامی است.", nameof(supersedesEvaluationRevisionId));
        }

        var normalizedReason = Normalize(correctionReason);
        if (normalizedReason is null || normalizedReason.Length > MaximumCorrectionReasonLength)
        {
            throw new ArgumentException("دلیل اصلاح الزامی و دارای طول معتبر است.", nameof(correctionReason));
        }

        ValidateValues(
            score,
            maxScore,
            learnerFeedback,
            guardianVisibleFeedback,
            teacherPrivateNote,
            createdAtUtc);

        return new EvaluationRevision(
            Guid.NewGuid(),
            organizationId,
            submissionAttemptId,
            revisionNumber,
            score,
            Normalize(learnerFeedback),
            Normalize(guardianVisibleFeedback),
            Normalize(teacherPrivateNote),
            createdByMembershipId,
            createdAtUtc,
            supersedesEvaluationRevisionId,
            normalizedReason);
    }

    public void UpdateDraft(
        decimal score,
        decimal maxScore,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? teacherPrivateNote,
        DateTime updatedAtUtc)
    {
        EnsureDraft();
        ValidateValues(
            score,
            maxScore,
            learnerFeedback,
            guardianVisibleFeedback,
            teacherPrivateNote,
            updatedAtUtc);
        Score = score;
        LearnerFeedback = Normalize(learnerFeedback);
        GuardianVisibleFeedback = Normalize(guardianVisibleFeedback);
        TeacherPrivateNote = Normalize(teacherPrivateNote);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Release(DateTime releasedAtUtc)
    {
        EnsureDraft();
        Status = EvaluationRevisionStatus.Released;
        UpdatedAtUtc = EnsureUtc(releasedAtUtc, nameof(releasedAtUtc));
    }

    public void MarkSuperseded(DateTime supersededAtUtc)
    {
        if (!IsReleased)
        {
            throw new InvalidOperationException("فقط ارزیابی منتشرشده می‌تواند با نسخه جدید جایگزین شود.");
        }

        Status = EvaluationRevisionStatus.Superseded;
        UpdatedAtUtc = EnsureUtc(supersededAtUtc, nameof(supersededAtUtc));
    }

    public static void ValidateScore(decimal score, decimal maxScore)
    {
        if (maxScore <= 0 || maxScore > AssignmentVersion.MaximumScoreValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxScore));
        }

        if (score < 0 ||
            score > maxScore ||
            score > AssignmentVersion.MaximumScoreValue ||
            decimal.Round(score, 2) != score)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                "نمره باید بین صفر و سقف نسخه تکلیف و دارای حداکثر دو رقم اعشار باشد.");
        }
    }

    private static void ValidateIdentity(
        Guid organizationId,
        Guid submissionAttemptId,
        Guid createdByMembershipId,
        int revisionNumber)
    {
        if (organizationId == Guid.Empty ||
            submissionAttemptId == Guid.Empty ||
            createdByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های ارزیابی الزامی هستند.");
        }

        if (revisionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }
    }

    private static void ValidateValues(
        decimal score,
        decimal maxScore,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? teacherPrivateNote,
        DateTime timestampUtc)
    {
        ValidateScore(score, maxScore);
        ValidateLength(learnerFeedback, MaximumFeedbackLength, nameof(learnerFeedback));
        ValidateLength(guardianVisibleFeedback, MaximumFeedbackLength, nameof(guardianVisibleFeedback));
        ValidateLength(teacherPrivateNote, MaximumPrivateNoteLength, nameof(teacherPrivateNote));
        _ = EnsureUtc(timestampUtc, nameof(timestampUtc));
    }

    private static void ValidateLength(string? value, int maximumLength, string parameterName)
    {
        if (value?.Trim().Length > maximumLength)
        {
            throw new ArgumentException("طول متن از حد مجاز بیشتر است.", parameterName);
        }
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("ارزیابی منتشرشده یا تاریخی قابل ویرایش مستقیم نیست.");
        }
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentException("زمان باید UTC باشد.", parameterName)
        };
}
