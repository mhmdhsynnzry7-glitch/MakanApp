namespace MakanApp.Domain.Assessment;

public sealed class ExamVersion
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 10_000;
    public const decimal MaximumScoreValue = 9_999_999.99m;

    private ExamVersion()
    {
    }

    private ExamVersion(
        Guid id,
        Guid organizationId,
        Guid examId,
        int versionNumber,
        string title,
        string? description,
        DateTime availableFromUtc,
        DateTime availableUntilUtc,
        int durationMinutes,
        int maxAttempts,
        decimal maxScore,
        ExamRandomizationPolicy randomizationPolicy,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ExamId = examId;
        VersionNumber = versionNumber;
        Title = title;
        Description = description;
        AvailableFromUtc = availableFromUtc;
        AvailableUntilUtc = availableUntilUtc;
        DurationMinutes = durationMinutes;
        MaxAttempts = maxAttempts;
        MaxScore = maxScore;
        RandomizationPolicy = randomizationPolicy;
        Status = ExamVersionStatus.Draft;
        CreatedByMembershipId = createdByMembershipId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExamId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime AvailableFromUtc { get; private set; }
    public DateTime AvailableUntilUtc { get; private set; }
    public int DurationMinutes { get; private set; }
    public int MaxAttempts { get; private set; }
    public decimal MaxScore { get; private set; }
    public ExamRandomizationPolicy RandomizationPolicy { get; private set; }
    public ExamVersionStatus Status { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public Guid CreatedByMembershipId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == ExamVersionStatus.Draft && !PublishedAtUtc.HasValue;
    public bool IsPublished => Status == ExamVersionStatus.Published && PublishedAtUtc.HasValue;

    public static ExamVersion CreateDraft(
        Guid organizationId,
        Guid examId,
        int versionNumber,
        string title,
        string? description,
        DateTime availableFromUtc,
        DateTime availableUntilUtc,
        int durationMinutes,
        int maxAttempts,
        decimal maxScore,
        ExamRandomizationPolicy randomizationPolicy,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        ValidateIdentity(organizationId, examId, createdByMembershipId, versionNumber);
        ValidateValues(
            title,
            description,
            availableFromUtc,
            availableUntilUtc,
            durationMinutes,
            maxAttempts,
            maxScore,
            randomizationPolicy,
            createdAtUtc);

        return new ExamVersion(
            Guid.NewGuid(),
            organizationId,
            examId,
            versionNumber,
            title.Trim(),
            Normalize(description),
            EnsureUtc(availableFromUtc, nameof(availableFromUtc)),
            EnsureUtc(availableUntilUtc, nameof(availableUntilUtc)),
            durationMinutes,
            maxAttempts,
            maxScore,
            randomizationPolicy,
            createdByMembershipId,
            EnsureUtc(createdAtUtc, nameof(createdAtUtc)));
    }

    public ExamVersion CreateNextDraft(
        int versionNumber,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        if (!IsPublished)
        {
            throw new InvalidOperationException("فقط نسخه منتشرشده می‌تواند مبنای نسخه بعدی باشد.");
        }

        return CreateDraft(
            OrganizationId,
            ExamId,
            versionNumber,
            Title,
            Description,
            AvailableFromUtc,
            AvailableUntilUtc,
            DurationMinutes,
            MaxAttempts,
            MaxScore,
            RandomizationPolicy,
            createdByMembershipId,
            createdAtUtc);
    }

    public void UpdateDraft(
        string title,
        string? description,
        DateTime availableFromUtc,
        DateTime availableUntilUtc,
        int durationMinutes,
        int maxAttempts,
        decimal maxScore,
        ExamRandomizationPolicy randomizationPolicy,
        DateTime updatedAtUtc)
    {
        EnsureDraft();
        ValidateValues(
            title,
            description,
            availableFromUtc,
            availableUntilUtc,
            durationMinutes,
            maxAttempts,
            maxScore,
            randomizationPolicy,
            updatedAtUtc);

        Title = title.Trim();
        Description = Normalize(description);
        AvailableFromUtc = EnsureUtc(availableFromUtc, nameof(availableFromUtc));
        AvailableUntilUtc = EnsureUtc(availableUntilUtc, nameof(availableUntilUtc));
        DurationMinutes = durationMinutes;
        MaxAttempts = maxAttempts;
        MaxScore = maxScore;
        RandomizationPolicy = randomizationPolicy;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void MarkQuestionsChanged(DateTime updatedAtUtc)
    {
        EnsureDraft();
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void Publish(IReadOnlyCollection<QuestionVersion> questions, DateTime publishedAtUtc)
    {
        EnsureDraft();
        publishedAtUtc = EnsureUtc(publishedAtUtc, nameof(publishedAtUtc));

        if (questions.Count == 0)
        {
            throw new ExamQuestionRequiredException();
        }

        foreach (var question in questions)
        {
            if (question.OrganizationId != OrganizationId || question.ExamVersionId != Id)
            {
                throw new InvalidOperationException("سؤال متعلق به نسخه جاری آزمون نیست.");
            }

            question.ValidateAnswerKey();
        }

        if (questions.Sum(question => question.Score) != MaxScore)
        {
            throw new ExamScoreTotalInvalidException();
        }

        Status = ExamVersionStatus.Published;
        PublishedAtUtc = publishedAtUtc;
        UpdatedAtUtc = publishedAtUtc;
    }

    private static void ValidateIdentity(
        Guid organizationId,
        Guid examId,
        Guid createdByMembershipId,
        int versionNumber)
    {
        if (organizationId == Guid.Empty || examId == Guid.Empty || createdByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های نسخه آزمون الزامی هستند.");
        }

        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        }
    }

    private static void ValidateValues(
        string title,
        string? description,
        DateTime availableFromUtc,
        DateTime availableUntilUtc,
        int durationMinutes,
        int maxAttempts,
        decimal maxScore,
        ExamRandomizationPolicy randomizationPolicy,
        DateTime timestampUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException("عنوان آزمون معتبر نیست.", nameof(title));
        }

        if (description?.Trim().Length > MaximumDescriptionLength)
        {
            throw new ArgumentException("توضیح آزمون از طول مجاز بیشتر است.", nameof(description));
        }

        _ = EnsureUtc(timestampUtc, nameof(timestampUtc));
        _ = EnsureUtc(availableFromUtc, nameof(availableFromUtc));
        _ = EnsureUtc(availableUntilUtc, nameof(availableUntilUtc));
        if (availableUntilUtc <= availableFromUtc)
        {
            throw new ArgumentException("پایان بازه آزمون باید بعد از شروع آن باشد.", nameof(availableUntilUtc));
        }

        if (durationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        }

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        if (maxScore <= 0 || maxScore > MaximumScoreValue || decimal.Round(maxScore, 2) != maxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(maxScore));
        }

        if (!Enum.IsDefined(randomizationPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(randomizationPolicy));
        }
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("نسخه منتشرشده آزمون قابل ویرایش نیست.");
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
            // SQL Server datetime2 اطلاعات Kind را نگه نمی‌دارد؛ مقدار round-trip همچنان UTC است.
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentException("زمان باید UTC باشد.", parameterName)
        };
}

public sealed class ExamQuestionRequiredException : Exception;

public sealed class ExamScoreTotalInvalidException : Exception;
