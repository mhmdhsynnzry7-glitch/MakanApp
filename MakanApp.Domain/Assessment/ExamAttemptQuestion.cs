namespace MakanApp.Domain.Assessment;

public sealed class ExamAttemptQuestion
{
    private ExamAttemptQuestion()
    {
    }

    private ExamAttemptQuestion(
        Guid id,
        Guid organizationId,
        Guid examAttemptId,
        Guid examVersionId,
        Guid questionVersionId,
        int displayOrder)
    {
        Id = id;
        OrganizationId = organizationId;
        ExamAttemptId = examAttemptId;
        ExamVersionId = examVersionId;
        QuestionVersionId = questionVersionId;
        DisplayOrder = displayOrder;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamVersionId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public int DisplayOrder { get; private set; }
    public Guid? CurrentAnswerRevisionId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ExamAttemptQuestion Create(
        ExamAttempt attempt,
        Guid questionVersionId,
        int displayOrder)
    {
        if (questionVersionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه نسخه سؤال الزامی است.", nameof(questionVersionId));
        }

        if (displayOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayOrder));
        }

        return new ExamAttemptQuestion(
            Guid.NewGuid(),
            attempt.OrganizationId,
            attempt.Id,
            attempt.ExamVersionId,
            questionVersionId,
            displayOrder);
    }

    public void AcceptAnswerRevision(AnswerRevision revision)
    {
        if (revision.OrganizationId != OrganizationId ||
            revision.ExamAttemptId != ExamAttemptId ||
            revision.ExamAttemptQuestionId != Id ||
            revision.SupersedesAnswerRevisionId != CurrentAnswerRevisionId)
        {
            throw new InvalidOperationException("نسخه پاسخ با وضعیت فعلی سؤال تلاش هم‌خوان نیست.");
        }

        CurrentAnswerRevisionId = revision.Id;
    }
}
