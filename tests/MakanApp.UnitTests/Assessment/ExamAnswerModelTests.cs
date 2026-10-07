using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class ExamAnswerModelTests
{
    [Fact]
    public void ValidObjectiveAnswerCreatesImmutableRevision()
    {
        var model = CreateModel(ExamQuestionType.ObjectiveSingleChoice);
        var optionId = model.Question.Options.First().Id;

        var revision = CreateObjective(model, optionId);

        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal(optionId, revision.SelectedOptionId);
        Assert.Null(revision.TextAnswer);
        Assert.Equal(1, revision.AcceptedWriteLeaseVersion);
        Assert.Equal(1, revision.AcceptedAnswerSetVersion);
    }

    [Fact]
    public void ObjectiveOptionFromAnotherQuestionIsRejected()
    {
        var model = CreateModel(ExamQuestionType.ObjectiveSingleChoice);

        Assert.Throws<ExamAnswerOptionInvalidException>(() =>
            CreateObjective(model, Guid.NewGuid()));
    }

    [Fact]
    public void DescriptiveAnswerPreservesExactTextIncludingEmptyAnswer()
    {
        var model = CreateModel(ExamQuestionType.Descriptive);
        const string text = "  پاسخ با فاصله  \n";

        var revision = CreateDescriptive(model, text);
        var cleared = CreateDescriptive(model, string.Empty, revisionNumber: 2);

        Assert.Equal(text, revision.TextAnswer);
        Assert.Equal(string.Empty, cleared.TextAnswer);
    }

    [Fact]
    public void OversizedDescriptiveAnswerIsRejected()
    {
        var model = CreateModel(ExamQuestionType.Descriptive);

        Assert.Throws<ExamAnswerTypeInvalidException>(() =>
            CreateDescriptive(model, new string('a', AnswerRevision.MaximumTextAnswerLength + 1)));
    }

    [Fact]
    public void DeadlinePassedAnswerIsRejected()
    {
        var model = CreateModel(ExamQuestionType.Descriptive);

        Assert.Throws<ExamAttemptDeadlinePassedException>(() =>
            model.Attempt.EnsureWriteLease(
                model.SessionId,
                model.Attempt.WriteLeaseVersion,
                model.Attempt.EffectiveDeadlineUtc));
    }

    [Fact]
    public void WriteRequiresLeaseAndOwnerSession()
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);

        Assert.Throws<ExamWriteLeaseRequiredException>(() =>
            attempt.EnsureWriteLease(Guid.NewGuid(), 0, nowUtc));

        var ownerSessionId = Guid.NewGuid();
        attempt.AcquireWriteLease(ownerSessionId, nowUtc);
        attempt.EnsureWriteLease(ownerSessionId, 1, nowUtc);
        Assert.Throws<ExamWriteLeaseHeldException>(() =>
            attempt.EnsureWriteLease(Guid.NewGuid(), 1, nowUtc));
    }

    [Fact]
    public void SecondSessionCannotAcquireWithoutExplicitTransfer()
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        attempt.AcquireWriteLease(Guid.NewGuid(), nowUtc);

        Assert.Throws<ExamWriteLeaseHeldException>(() =>
            attempt.AcquireWriteLease(Guid.NewGuid(), nowUtc));
    }

    [Fact]
    public void TransferIncrementsLeaseVersionAndInvalidatesOldLease()
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        attempt.AcquireWriteLease(firstSessionId, nowUtc);

        attempt.TransferWriteLease(secondSessionId, nowUtc.AddSeconds(1));

        Assert.Equal(2, attempt.WriteLeaseVersion);
        Assert.Throws<ExamWriteLeaseStaleException>(() =>
            attempt.EnsureWriteLease(firstSessionId, 1, nowUtc.AddSeconds(2)));
        attempt.EnsureWriteLease(secondSessionId, 2, nowUtc.AddSeconds(2));
    }

    [Fact]
    public void AcceptedMutationsIncreaseAnswerSetVersionMonotonically()
    {
        var attempt = Start(UtcNow());

        Assert.Equal(1, attempt.AcceptAnswerMutation());
        Assert.Equal(2, attempt.AcceptAnswerMutation());
        Assert.Equal(2, attempt.AnswerSetVersion);
    }

    [Fact]
    public void StaleAnswerRevisionCannotReplaceCurrentPointer()
    {
        var model = CreateModel(ExamQuestionType.ObjectiveSingleChoice);
        var first = CreateObjective(model, model.Question.Options.First().Id);
        model.AttemptQuestion.AcceptAnswerRevision(first);
        var stale = CreateObjective(
            model,
            model.Question.Options.Last().Id,
            revisionNumber: 2,
            supersedesRevisionId: null);

        Assert.Throws<InvalidOperationException>(() =>
            model.AttemptQuestion.AcceptAnswerRevision(stale));
        Assert.Equal(first.Id, model.AttemptQuestion.CurrentAnswerRevisionId);
    }

    [Fact]
    public void StudentAnswerContractsContainNoAnswerKeyOrGradeFields()
    {
        var names = typeof(ExamAnswerReceiptDto)
            .GetProperties()
            .Concat(typeof(StudentExamAnswersDto).GetProperties())
            .Concat(typeof(StudentCurrentExamAnswerDto).GetProperties())
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(names, name => name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("AnswerKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Score", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveCommandDoesNotAcceptClientTimestampOrServerControlledVersions()
    {
        var names = typeof(SaveExamAnswerCommand)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(names, name => name.Contains("CreatedAt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("AcceptedAt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("RevisionNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("AnswerSetVersion", StringComparison.OrdinalIgnoreCase));
    }

    private static AnswerModel CreateModel(ExamQuestionType type)
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        var options = type == ExamQuestionType.ObjectiveSingleChoice
            ? new[]
            {
                new ExamQuestionOptionDefinition(1, "A", true),
                new ExamQuestionOptionDefinition(2, "B", false)
            }
            : null;
        var question = QuestionVersion.CreateDraft(
            attempt.OrganizationId,
            attempt.ExamVersionId,
            1,
            type,
            "Question",
            10m,
            options,
            nowUtc);
        var attemptQuestion = ExamAttemptQuestion.Create(attempt, question.Id, 1);
        var sessionId = Guid.NewGuid();
        attempt.AcquireWriteLease(sessionId, nowUtc);
        return new AnswerModel(attempt, attemptQuestion, question, sessionId, nowUtc);
    }

    private static AnswerRevision CreateObjective(
        AnswerModel model,
        Guid optionId,
        int revisionNumber = 1,
        Guid? supersedesRevisionId = null) =>
        AnswerRevision.CreateObjective(
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            optionId,
            revisionNumber,
            Guid.NewGuid(),
            new string('A', AnswerRevision.RequestHashLength),
            model.SessionId,
            supersedesRevisionId,
            model.Attempt.WriteLeaseVersion,
            revisionNumber,
            model.NowUtc);

    private static AnswerRevision CreateDescriptive(
        AnswerModel model,
        string text,
        int revisionNumber = 1) =>
        AnswerRevision.CreateDescriptive(
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            text,
            revisionNumber,
            Guid.NewGuid(),
            new string('B', AnswerRevision.RequestHashLength),
            model.SessionId,
            null,
            model.Attempt.WriteLeaseVersion,
            revisionNumber,
            model.NowUtc);

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

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

    private sealed record AnswerModel(
        ExamAttempt Attempt,
        ExamAttemptQuestion AttemptQuestion,
        QuestionVersion Question,
        Guid SessionId,
        DateTime NowUtc);
}
