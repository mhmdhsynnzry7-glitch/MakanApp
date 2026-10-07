namespace MakanApp.Domain.Assessment;

public sealed class ExamAttempt
{
    private ExamAttempt()
    {
    }

    private ExamAttempt(
        Guid id,
        Guid organizationId,
        Guid examId,
        Guid examVersionId,
        Guid classId,
        Guid enrollmentId,
        Guid clientOperationId,
        int attemptNumber,
        DateTime startedAtUtc,
        DateTime effectiveDeadlineUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ExamId = examId;
        ExamVersionId = examVersionId;
        ClassId = classId;
        EnrollmentId = enrollmentId;
        ClientOperationId = clientOperationId;
        AttemptNumber = attemptNumber;
        Status = ExamAttemptStatus.InProgress;
        StartedAtUtc = startedAtUtc;
        EffectiveDeadlineUtc = effectiveDeadlineUtc;
        CreatedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExamId { get; private set; }
    public Guid ExamVersionId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public int AttemptNumber { get; private set; }
    public ExamAttemptStatus Status { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime EffectiveDeadlineUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExpiredAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsInProgress => Status == ExamAttemptStatus.InProgress;

    public static ExamAttempt Start(
        Guid organizationId,
        Guid examId,
        Guid examVersionId,
        Guid classId,
        Guid enrollmentId,
        Guid clientOperationId,
        int attemptNumber,
        int maxAttempts,
        DateTime availableFromUtc,
        DateTime availableUntilUtc,
        int durationMinutes,
        DateTime startedAtUtc)
    {
        ValidateIdentifiers(
            organizationId,
            examId,
            examVersionId,
            classId,
            enrollmentId,
            clientOperationId);
        startedAtUtc = EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        availableFromUtc = EnsureUtc(availableFromUtc, nameof(availableFromUtc));
        availableUntilUtc = EnsureUtc(availableUntilUtc, nameof(availableUntilUtc));

        if (startedAtUtc < availableFromUtc)
        {
            throw new ExamNotAvailableYetException();
        }

        if (startedAtUtc >= availableUntilUtc)
        {
            throw new ExamWindowClosedException();
        }

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        }

        if (attemptNumber <= 0 || maxAttempts <= 0 || attemptNumber > maxAttempts)
        {
            throw new ExamAttemptsExhaustedException();
        }

        var durationDeadlineUtc = startedAtUtc.AddMinutes(durationMinutes);
        var effectiveDeadlineUtc = durationDeadlineUtc < availableUntilUtc
            ? durationDeadlineUtc
            : availableUntilUtc;

        return new ExamAttempt(
            Guid.NewGuid(),
            organizationId,
            examId,
            examVersionId,
            classId,
            enrollmentId,
            clientOperationId,
            attemptNumber,
            startedAtUtc,
            effectiveDeadlineUtc);
    }

    public void MarkExpired(DateTime expiredAtUtc)
    {
        expiredAtUtc = EnsureUtc(expiredAtUtc, nameof(expiredAtUtc));
        if (!IsInProgress || expiredAtUtc < EffectiveDeadlineUtc)
        {
            throw new InvalidOperationException("تلاش آزمون هنوز قابل انقضا نیست.");
        }

        Status = ExamAttemptStatus.Expired;
        ExpiredAtUtc = expiredAtUtc;
    }

    private static void ValidateIdentifiers(params Guid[] identifiers)
    {
        if (identifiers.Any(identifier => identifier == Guid.Empty))
        {
            throw new ArgumentException("شناسه‌های تلاش آزمون الزامی هستند.");
        }
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentException("زمان باید UTC باشد.", parameterName)
        };
}

public sealed class ExamNotAvailableYetException : Exception;
public sealed class ExamWindowClosedException : Exception;
public sealed class ExamAttemptsExhaustedException : Exception;
