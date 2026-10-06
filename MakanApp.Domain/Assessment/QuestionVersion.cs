namespace MakanApp.Domain.Assessment;

public sealed record ExamQuestionOptionDefinition(int Order, string Text, bool IsCorrect);

public sealed class QuestionVersion
{
    public const int MaximumPromptLength = 20_000;

    private readonly List<QuestionOption> _options = [];

    private QuestionVersion()
    {
    }

    private QuestionVersion(
        Guid id,
        Guid organizationId,
        Guid examVersionId,
        int order,
        ExamQuestionType type,
        string prompt,
        decimal score,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ExamVersionId = examVersionId;
        Order = order;
        Type = type;
        Prompt = prompt;
        Score = score;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExamVersionId { get; private set; }
    public int Order { get; private set; }
    public ExamQuestionType Type { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public decimal Score { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<QuestionOption> Options => _options;

    public static QuestionVersion CreateDraft(
        Guid organizationId,
        Guid examVersionId,
        int order,
        ExamQuestionType type,
        string prompt,
        decimal score,
        IEnumerable<ExamQuestionOptionDefinition>? options,
        DateTime createdAtUtc)
    {
        ValidateValues(organizationId, examVersionId, order, type, prompt, score, createdAtUtc);
        var question = new QuestionVersion(
            Guid.NewGuid(),
            organizationId,
            examVersionId,
            order,
            type,
            prompt.Trim(),
            score,
            createdAtUtc);
        question.ReplaceOptions(options);
        question.ValidateAnswerKey();
        return question;
    }

    public QuestionVersion CopyTo(Guid examVersionId, DateTime createdAtUtc) =>
        CreateDraft(
            OrganizationId,
            examVersionId,
            Order,
            Type,
            Prompt,
            Score,
            _options.Select(option => new ExamQuestionOptionDefinition(
                option.Order,
                option.Text,
                option.IsCorrect)),
            createdAtUtc);

    public void UpdateDraft(
        ExamVersion examVersion,
        int order,
        ExamQuestionType type,
        string prompt,
        decimal score,
        IEnumerable<ExamQuestionOptionDefinition>? options,
        DateTime updatedAtUtc)
    {
        if (!examVersion.IsDraft ||
            examVersion.Id != ExamVersionId ||
            examVersion.OrganizationId != OrganizationId)
        {
            throw new InvalidOperationException("سؤال نسخه منتشرشده قابل ویرایش نیست.");
        }

        ValidateValues(OrganizationId, ExamVersionId, order, type, prompt, score, updatedAtUtc);
        Order = order;
        Type = type;
        Prompt = prompt.Trim();
        Score = score;
        ReplaceOptions(options);
        ValidateAnswerKey();
    }

    public void ValidateAnswerKey()
    {
        if (Type == ExamQuestionType.Descriptive)
        {
            if (_options.Count != 0)
            {
                throw new ExamAnswerKeyInvalidException();
            }

            return;
        }

        if (_options.Count < 2 ||
            _options.Count(option => option.IsCorrect) != 1 ||
            _options.Select(option => option.Order).Distinct().Count() != _options.Count ||
            _options.Select(option => option.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _options.Count)
        {
            throw new ExamAnswerKeyInvalidException();
        }
    }

    private void ReplaceOptions(IEnumerable<ExamQuestionOptionDefinition>? definitions)
    {
        _options.Clear();
        if (definitions is null)
        {
            return;
        }

        foreach (var definition in definitions)
        {
            _options.Add(QuestionOption.Create(OrganizationId, Id, definition));
        }
    }

    private static void ValidateValues(
        Guid organizationId,
        Guid examVersionId,
        int order,
        ExamQuestionType type,
        string prompt,
        decimal score,
        DateTime timestampUtc)
    {
        if (organizationId == Guid.Empty || examVersionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های سؤال الزامی هستند.");
        }

        if (order <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        var normalizedPrompt = prompt?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPrompt) || normalizedPrompt.Length > MaximumPromptLength)
        {
            throw new ArgumentException("متن سؤال معتبر نیست.", nameof(prompt));
        }

        if (score <= 0 || score > ExamVersion.MaximumScoreValue || decimal.Round(score, 2) != score)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان باید UTC باشد.", nameof(timestampUtc));
        }
    }
}

public sealed class ExamAnswerKeyInvalidException : Exception;
