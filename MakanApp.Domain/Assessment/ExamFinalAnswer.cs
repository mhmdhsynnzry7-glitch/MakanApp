namespace MakanApp.Domain.Assessment;

public sealed class ExamFinalAnswer
{
    private ExamFinalAnswer()
    {
    }

    private ExamFinalAnswer(
        Guid organizationId,
        Guid examAttemptId,
        Guid examAttemptQuestionId,
        Guid questionVersionId,
        Guid answerRevisionId)
    {
        OrganizationId = organizationId;
        ExamAttemptId = examAttemptId;
        ExamAttemptQuestionId = examAttemptQuestionId;
        QuestionVersionId = questionVersionId;
        AnswerRevisionId = answerRevisionId;
    }

    public Guid OrganizationId { get; private set; }
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamAttemptQuestionId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public Guid AnswerRevisionId { get; private set; }

    public static ExamFinalAnswer Create(
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        AnswerRevision revision)
    {
        if (!attempt.IsInProgress ||
            attemptQuestion.OrganizationId != attempt.OrganizationId ||
            attemptQuestion.ExamAttemptId != attempt.Id ||
            revision.OrganizationId != attempt.OrganizationId ||
            revision.ExamAttemptId != attempt.Id ||
            revision.ExamAttemptQuestionId != attemptQuestion.Id ||
            revision.QuestionVersionId != attemptQuestion.QuestionVersionId ||
            attemptQuestion.CurrentAnswerRevisionId != revision.Id)
        {
            throw new InvalidOperationException("پاسخ نهایی باید همان پاسخ پذیرفته‌شده جاری سؤال تلاش باشد.");
        }

        return new ExamFinalAnswer(
            attempt.OrganizationId,
            attempt.Id,
            attemptQuestion.Id,
            attemptQuestion.QuestionVersionId,
            revision.Id);
    }
}
