using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class ExamGradingModelTests
{
    [Fact]
    public void CorrectObjectiveAnswerReceivesFullPoints()
    {
        var model = CreateObjectiveModel(selectCorrect: true);

        var grade = CreateQuestionGrade(model);

        Assert.Equal(model.Question.Score, grade.AwardedScore);
        Assert.True(grade.IsReviewed);
        Assert.Equal(ExamQuestionGradingMode.Objective, grade.GradingMode);
    }

    [Fact]
    public void IncorrectObjectiveAnswerReceivesZero()
    {
        var model = CreateObjectiveModel(selectCorrect: false);

        var grade = CreateQuestionGrade(model);

        Assert.Equal(0, grade.AwardedScore);
        Assert.True(grade.IsAnswered);
    }

    [Fact]
    public void UnansweredObjectiveQuestionReceivesZeroWithoutFabricatedRevision()
    {
        var model = CreateObjectiveModel(withAnswer: false);

        var grade = CreateQuestionGrade(model);

        Assert.Equal(0, grade.AwardedScore);
        Assert.False(grade.IsAnswered);
        Assert.Null(model.Revision);
    }

    [Fact]
    public void DescriptiveScoreBelowZeroIsRejected()
    {
        var model = CreateDescriptiveModel();
        var grade = CreateQuestionGrade(model);

        Assert.Throws<ArgumentOutOfRangeException>(() => grade.ReviewManually(
            -0.01m,
            null,
            null,
            Guid.NewGuid(),
            model.NowUtc.AddMinutes(1)));
    }

    [Fact]
    public void DescriptiveScoreAboveQuestionMaximumIsRejected()
    {
        var model = CreateDescriptiveModel();
        var grade = CreateQuestionGrade(model);

        Assert.Throws<ArgumentOutOfRangeException>(() => grade.ReviewManually(
            model.Question.Score + 0.01m,
            null,
            null,
            Guid.NewGuid(),
            model.NowUtc.AddMinutes(1)));
    }

    [Fact]
    public void TotalScoreIsCalculatedFromQuestionGrades()
    {
        var model = CreateObjectiveModel(selectCorrect: true);
        var revision = ExamGradeRevision.CreateDraft(
            model.Attempt.OrganizationId,
            model.Attempt.Id,
            Guid.NewGuid(),
            model.NowUtc);
        var first = ExamQuestionGrade.Create(
            revision,
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            model.FinalAnswer,
            model.Revision,
            Guid.NewGuid(),
            model.NowUtc);

        revision.Recalculate([first], model.Question.Score, model.NowUtc.AddMinutes(1));

        Assert.Equal(model.Question.Score, revision.TotalScore);
        Assert.Equal(ExamGradeRevisionStatus.Draft, revision.Status);
    }

    [Fact]
    public void IncompleteManualReviewCannotBecomeReadyOrRelease()
    {
        var model = CreateDescriptiveModel();
        var revision = ExamGradeRevision.CreateDraft(
            model.Attempt.OrganizationId,
            model.Attempt.Id,
            Guid.NewGuid(),
            model.NowUtc);
        var grade = ExamQuestionGrade.Create(
            revision,
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            model.FinalAnswer,
            model.Revision,
            Guid.NewGuid(),
            model.NowUtc);

        Assert.Throws<ExamGradeIncompleteException>(() => revision.CompleteReview(
            [grade],
            model.Question.Score,
            null,
            null,
            null,
            model.NowUtc.AddMinutes(1)));
        Assert.Throws<ExamGradeIncompleteException>(() => revision.Release(
            [grade],
            model.Question.Score,
            model.NowUtc.AddMinutes(2)));
    }

    [Fact]
    public void CompletedGradeIsStillNotReleasedUntilExplicitRelease()
    {
        var (revision, grade, model) = CreateReviewedDescriptiveGrade(4m);

        revision.CompleteReview(
            [grade],
            model.Question.Score,
            "بازخورد",
            "بازخورد والد",
            "یادداشت خصوصی",
            model.NowUtc.AddMinutes(2));

        Assert.Equal(ExamGradeRevisionStatus.ReadyForRelease, revision.Status);
        Assert.False(revision.IsReleased);
    }

    [Fact]
    public void ExplicitReleasePreservesServerCalculatedTotal()
    {
        var (revision, grade, model) = CreateReviewedDescriptiveGrade(4m);
        revision.CompleteReview(
            [grade],
            model.Question.Score,
            null,
            null,
            null,
            model.NowUtc.AddMinutes(2));

        revision.Release([grade], model.Question.Score, model.NowUtc.AddMinutes(3));

        Assert.True(revision.IsReleased);
        Assert.Equal(4m, revision.TotalScore);
    }

    [Fact]
    public void CorrectionCreatesNewRevisionWithoutMutatingReleasedHistory()
    {
        var (released, grade, model) = CreateReviewedDescriptiveGrade(4m);
        released.CompleteReview(
            [grade],
            model.Question.Score,
            "نسخه اول",
            null,
            null,
            model.NowUtc.AddMinutes(2));
        released.Release([grade], model.Question.Score, model.NowUtc.AddMinutes(3));

        var correction = ExamGradeRevision.CreateCorrection(
            released,
            Guid.NewGuid(),
            "اصلاح نمره تشریحی",
            model.NowUtc.AddMinutes(4));

        Assert.Equal(2, correction.RevisionNumber);
        Assert.Equal(released.Id, correction.SupersedesExamGradeRevisionId);
        Assert.Equal(ExamGradeRevisionStatus.Draft, correction.Status);
        Assert.Equal(ExamGradeRevisionStatus.Released, released.Status);
    }

    [Fact]
    public void StudentAndGuardianResultContractsContainNoPrivateNoteOrAnswerKey()
    {
        var resultProperties = typeof(StudentExamResultDto)
            .GetProperties()
            .Concat(typeof(GuardianExamResultDto).GetProperties())
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(resultProperties, name =>
            name.Contains("Private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(resultProperties, name =>
            name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(resultProperties, name =>
            name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ObjectiveGradeCannotBeOverriddenManually()
    {
        var model = CreateObjectiveModel(selectCorrect: true);
        var grade = CreateQuestionGrade(model);

        Assert.Throws<InvalidOperationException>(() => grade.ReviewManually(
            0,
            null,
            null,
            Guid.NewGuid(),
            model.NowUtc.AddMinutes(1)));
    }

    private static ExamQuestionGrade CreateQuestionGrade(GradingModel model)
    {
        var revision = ExamGradeRevision.CreateDraft(
            model.Attempt.OrganizationId,
            model.Attempt.Id,
            Guid.NewGuid(),
            model.NowUtc);
        return ExamQuestionGrade.Create(
            revision,
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            model.FinalAnswer,
            model.Revision,
            Guid.NewGuid(),
            model.NowUtc);
    }

    private static (ExamGradeRevision Revision, ExamQuestionGrade Grade, GradingModel Model)
        CreateReviewedDescriptiveGrade(decimal score)
    {
        var model = CreateDescriptiveModel();
        var revision = ExamGradeRevision.CreateDraft(
            model.Attempt.OrganizationId,
            model.Attempt.Id,
            Guid.NewGuid(),
            model.NowUtc);
        var grade = ExamQuestionGrade.Create(
            revision,
            model.Attempt,
            model.AttemptQuestion,
            model.Question,
            model.FinalAnswer,
            model.Revision,
            Guid.NewGuid(),
            model.NowUtc);
        grade.ReviewManually(
            score,
            null,
            null,
            Guid.NewGuid(),
            model.NowUtc.AddMinutes(1));
        return (revision, grade, model);
    }

    private static GradingModel CreateObjectiveModel(
        bool selectCorrect = true,
        bool withAnswer = true)
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        var question = QuestionVersion.CreateDraft(
            attempt.OrganizationId,
            attempt.ExamVersionId,
            1,
            ExamQuestionType.ObjectiveSingleChoice,
            "سؤال objective",
            5m,
            [
                new ExamQuestionOptionDefinition(1, "درست", true),
                new ExamQuestionOptionDefinition(2, "نادرست", false)
            ],
            nowUtc);
        return FinalizeModel(attempt, question, withAnswer
            ? (mapping, sessionId) => AnswerRevision.CreateObjective(
                attempt,
                mapping,
                question,
                selectCorrect ? question.Options.First().Id : question.Options.Last().Id,
                1,
                Guid.NewGuid(),
                new string('A', AnswerRevision.RequestHashLength),
                sessionId,
                null,
                1,
                1,
                nowUtc)
            : null);
    }

    private static GradingModel CreateDescriptiveModel()
    {
        var nowUtc = UtcNow();
        var attempt = Start(nowUtc);
        var question = QuestionVersion.CreateDraft(
            attempt.OrganizationId,
            attempt.ExamVersionId,
            1,
            ExamQuestionType.Descriptive,
            "سؤال تشریحی",
            5m,
            null,
            nowUtc);
        return FinalizeModel(
            attempt,
            question,
            (mapping, sessionId) => AnswerRevision.CreateDescriptive(
                attempt,
                mapping,
                question,
                "پاسخ",
                1,
                Guid.NewGuid(),
                new string('A', AnswerRevision.RequestHashLength),
                sessionId,
                null,
                1,
                1,
                nowUtc));
    }

    private static GradingModel FinalizeModel(
        ExamAttempt attempt,
        QuestionVersion question,
        Func<ExamAttemptQuestion, Guid, AnswerRevision>? createRevision)
    {
        var mapping = ExamAttemptQuestion.Create(attempt, question.Id, 1);
        var sessionId = Guid.NewGuid();
        attempt.AcquireWriteLease(sessionId, attempt.StartedAtUtc);
        AnswerRevision? revision = null;
        if (createRevision is not null)
        {
            revision = createRevision(mapping, sessionId);
            _ = attempt.AcceptAnswerMutation();
            mapping.AcceptAnswerRevision(revision);
        }

        var finalAnswer = revision is null
            ? null
            : ExamFinalAnswer.Create(attempt, mapping, revision);

        attempt.Finalize(
            sessionId,
            1,
            revision is null ? 0 : 1,
            Guid.NewGuid(),
            new string('B', ExamAttempt.FinalizeRequestHashLength),
            attempt.StartedAtUtc.AddMinutes(1));
        return new GradingModel(
            attempt,
            mapping,
            question,
            finalAnswer,
            revision,
            attempt.StartedAtUtc.AddMinutes(1));
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

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

    private sealed record GradingModel(
        ExamAttempt Attempt,
        ExamAttemptQuestion AttemptQuestion,
        QuestionVersion Question,
        ExamFinalAnswer? FinalAnswer,
        AnswerRevision? Revision,
        DateTime NowUtc);
}
