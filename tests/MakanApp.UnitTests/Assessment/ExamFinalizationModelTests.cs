using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class ExamFinalizationModelTests
{
    [Fact]
    public void InProgressAttemptFinalizesWithServerValues()
    {
        var model = CreateAnsweredModel();
        var finalizedAtUtc = model.NowUtc.AddMinutes(1);
        var operationId = Guid.NewGuid();

        model.Attempt.Finalize(
            model.SessionId,
            1,
            1,
            operationId,
            RequestHash(),
            finalizedAtUtc);

        Assert.Equal(ExamAttemptStatus.Finalized, model.Attempt.Status);
        Assert.Equal(finalizedAtUtc, model.Attempt.FinalizedAtUtc);
        Assert.Equal(1, model.Attempt.FinalizedAnswerSetVersion);
        Assert.Equal(operationId, model.Attempt.FinalizeClientOperationId);
        Assert.Equal(model.SessionId, model.Attempt.FinalizedBySessionId);
    }

    [Fact]
    public void ExpectedAnswerSetVersionMustMatch()
    {
        var model = CreateAnsweredModel();

        Assert.Throws<ExamAnswerSetVersionConflictException>(() =>
            model.Attempt.Finalize(
                model.SessionId,
                1,
                0,
                Guid.NewGuid(),
                RequestHash(),
                model.NowUtc.AddMinutes(1)));
        Assert.Equal(ExamAttemptStatus.InProgress, model.Attempt.Status);
    }

    [Fact]
    public void FinalizeAtExactDeadlineIsRejected()
    {
        var model = CreateAnsweredModel();

        Assert.Throws<ExamAttemptDeadlinePassedException>(() =>
            model.Attempt.Finalize(
                model.SessionId,
                1,
                1,
                Guid.NewGuid(),
                RequestHash(),
                model.Attempt.EffectiveDeadlineUtc));
    }

    [Fact]
    public void FinalizedAttemptCannotMutateOrAcquireLease()
    {
        var model = CreateAnsweredModel();
        model.Attempt.Finalize(
            model.SessionId,
            1,
            1,
            Guid.NewGuid(),
            RequestHash(),
            model.NowUtc.AddMinutes(1));

        Assert.Throws<ExamAttemptNotWritableException>(() =>
            model.Attempt.EnsureWriteLease(model.SessionId, 1, model.NowUtc.AddMinutes(2)));
        Assert.Throws<ExamAttemptNotWritableException>(() =>
            model.Attempt.AcquireWriteLease(model.SessionId, model.NowUtc.AddMinutes(2)));
        Assert.Throws<ExamAttemptNotWritableException>(() =>
            model.Attempt.TransferWriteLease(Guid.NewGuid(), model.NowUtc.AddMinutes(2)));
    }

    [Fact]
    public void FinalAnswerReferencesExactCurrentRevision()
    {
        var model = CreateAnsweredModel();

        var finalAnswer = ExamFinalAnswer.Create(
            model.Attempt,
            model.AttemptQuestion,
            model.Revision);

        Assert.Equal(model.Attempt.Id, finalAnswer.ExamAttemptId);
        Assert.Equal(model.AttemptQuestion.Id, finalAnswer.ExamAttemptQuestionId);
        Assert.Equal(model.Revision.Id, finalAnswer.AnswerRevisionId);
    }

    [Fact]
    public void NonCurrentRevisionCannotBecomeOfficialFinalAnswer()
    {
        var model = CreateAnsweredModel();
        var stale = AnswerRevision.CreateObjective(
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            model.Question.Options.Last().Id,
            2,
            Guid.NewGuid(),
            RequestHash(),
            model.SessionId,
            model.Revision.Id,
            1,
            2,
            model.NowUtc.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() =>
            ExamFinalAnswer.Create(model.Attempt, model.AttemptQuestion, stale));
    }

    [Fact]
    public void EmptyDescriptiveRevisionIsFrozenButNotCountedAsAnswered()
    {
        var model = CreateDescriptiveModel(string.Empty);

        var finalAnswer = ExamFinalAnswer.Create(
            model.Attempt,
            model.AttemptQuestion,
            model.Revision);

        Assert.False(model.Revision.HasAnswer);
        Assert.Equal(model.Revision.Id, finalAnswer.AnswerRevisionId);
    }

    [Fact]
    public void FinalReceiptContractContainsNoGradingFields()
    {
        var names = typeof(ExamFinalReceiptDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(names, name => name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Score", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Grade", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Feedback", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("AnswerKey", StringComparison.OrdinalIgnoreCase));
    }

    private static FinalizationModel CreateAnsweredModel()
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        var question = QuestionVersion.CreateDraft(
            attempt.OrganizationId,
            attempt.ExamVersionId,
            1,
            ExamQuestionType.ObjectiveSingleChoice,
            "Question",
            10m,
            [
                new ExamQuestionOptionDefinition(1, "A", true),
                new ExamQuestionOptionDefinition(2, "B", false)
            ],
            nowUtc);
        return CreateModel(
            attempt,
            question,
            (mapping, sessionId) => AnswerRevision.CreateObjective(
                attempt,
                mapping,
                question,
                question.Options.First().Id,
                1,
                Guid.NewGuid(),
                RequestHash(),
                sessionId,
                null,
                1,
                1,
                nowUtc));
    }

    private static FinalizationModel CreateDescriptiveModel(string text)
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        var question = QuestionVersion.CreateDraft(
            attempt.OrganizationId,
            attempt.ExamVersionId,
            1,
            ExamQuestionType.Descriptive,
            "Question",
            10m,
            null,
            nowUtc);
        return CreateModel(
            attempt,
            question,
            (mapping, sessionId) => AnswerRevision.CreateDescriptive(
                attempt,
                mapping,
                question,
                text,
                1,
                Guid.NewGuid(),
                RequestHash(),
                sessionId,
                null,
                1,
                1,
                nowUtc));
    }

    private static FinalizationModel CreateModel(
        ExamAttempt attempt,
        QuestionVersion question,
        Func<ExamAttemptQuestion, Guid, AnswerRevision> createRevision)
    {
        var mapping = ExamAttemptQuestion.Create(attempt, question.Id, 1);
        var sessionId = Guid.NewGuid();
        attempt.AcquireWriteLease(sessionId, attempt.StartedAtUtc);
        var revision = createRevision(mapping, sessionId);
        Assert.Equal(1, attempt.AcceptAnswerMutation());
        mapping.AcceptAnswerRevision(revision);
        return new FinalizationModel(
            attempt,
            mapping,
            question,
            revision,
            sessionId,
            attempt.StartedAtUtc);
    }

    private static ExamAttempt Start(DateTime nowUtc) =>
        ExamAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            1,
            nowUtc.AddMinutes(-5),
            nowUtc.AddHours(1),
            45,
            nowUtc);

    private static string RequestHash() => new('A', ExamAttempt.FinalizeRequestHashLength);

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

    private sealed record FinalizationModel(
        ExamAttempt Attempt,
        ExamAttemptQuestion AttemptQuestion,
        QuestionVersion Question,
        AnswerRevision Revision,
        Guid SessionId,
        DateTime NowUtc);
}
