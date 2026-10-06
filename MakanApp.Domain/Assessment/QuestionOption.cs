namespace MakanApp.Domain.Assessment;

public sealed class QuestionOption
{
    public const int MaximumTextLength = 2_000;

    private QuestionOption()
    {
    }

    private QuestionOption(
        Guid id,
        Guid organizationId,
        Guid questionVersionId,
        int order,
        string text,
        bool isCorrect)
    {
        Id = id;
        OrganizationId = organizationId;
        QuestionVersionId = questionVersionId;
        Order = order;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public int Order { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }

    internal static QuestionOption Create(
        Guid organizationId,
        Guid questionVersionId,
        ExamQuestionOptionDefinition definition)
    {
        var normalizedText = definition.Text?.Trim();
        if (organizationId == Guid.Empty || questionVersionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های گزینه سؤال الزامی هستند.");
        }

        if (definition.Order <= 0 ||
            string.IsNullOrWhiteSpace(normalizedText) ||
            normalizedText.Length > MaximumTextLength)
        {
            throw new ExamAnswerKeyInvalidException();
        }

        return new QuestionOption(
            Guid.NewGuid(),
            organizationId,
            questionVersionId,
            definition.Order,
            normalizedText,
            definition.IsCorrect);
    }
}
