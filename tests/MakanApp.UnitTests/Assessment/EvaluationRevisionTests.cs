using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class EvaluationRevisionTests
{
    [Fact]
    public void NewEvaluationIsDraftAndNotReleased()
    {
        var evaluation = CreateDraft(17m, 20m);

        Assert.Equal(EvaluationRevisionStatus.Draft, evaluation.Status);
        Assert.True(evaluation.IsDraft);
        Assert.False(evaluation.IsReleased);
    }

    [Fact]
    public void NegativeScoreIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDraft(-0.01m, 20m));
    }

    [Fact]
    public void ScoreAboveMaximumIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDraft(20.01m, 20m));
    }

    [Fact]
    public void ScoreEqualToMaximumIsAccepted()
    {
        Assert.Equal(20m, CreateDraft(20m, 20m).Score);
    }

    [Fact]
    public void DecimalScoreWithinRangeIsAccepted()
    {
        Assert.Equal(17.25m, CreateDraft(17.25m, 20m).Score);
    }

    [Fact]
    public void VersionWithMaximumTenRejectsEleven()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDraft(11m, 10m));
    }

    [Fact]
    public void VersionWithMaximumTwentyAcceptsEighteen()
    {
        Assert.Equal(18m, CreateDraft(18m, 20m).Score);
    }

    [Fact]
    public void ScoreWithMoreThanTwoDecimalPlacesIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDraft(17.251m, 20m));
    }

    [Fact]
    public void ReleasedEvaluationCannotBeEditedDirectly()
    {
        var evaluation = CreateDraft(17m, 20m);
        evaluation.Release(UtcNow());

        Assert.Throws<InvalidOperationException>(() => evaluation.UpdateDraft(
            18m,
            20m,
            "بازخورد جدید",
            "بازخورد والد",
            "یادداشت خصوصی",
            UtcNow()));
    }

    [Fact]
    public void CorrectionRequiresReasonAndCreatesNewRevision()
    {
        var released = CreateDraft(15m, 20m);
        released.Release(UtcNow());

        Assert.Throws<ArgumentException>(() => EvaluationRevision.CreateCorrection(
            released.OrganizationId,
            released.SubmissionAttemptId,
            2,
            17m,
            20m,
            null,
            null,
            null,
            Guid.NewGuid(),
            UtcNow(),
            released.Id,
            " "));

        var correction = EvaluationRevision.CreateCorrection(
            released.OrganizationId,
            released.SubmissionAttemptId,
            2,
            17m,
            20m,
            "اصلاح شد",
            "بهبود نتیجه",
            "بازبینی محاسبه",
            Guid.NewGuid(),
            UtcNow(),
            released.Id,
            "اصلاح محاسبه نمره");

        Assert.Equal(2, correction.RevisionNumber);
        Assert.Equal(released.Id, correction.SupersedesEvaluationRevisionId);
        Assert.Equal(EvaluationRevisionStatus.Draft, correction.Status);
        Assert.Equal(EvaluationRevisionStatus.Released, released.Status);
    }

    [Fact]
    public void PrivateNoteIsNeverCopiedToAudienceFeedback()
    {
        const string privateNote = "PRIVATE-DISTINCTIVE-NOTE";
        var evaluation = EvaluationRevision.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            18m,
            20m,
            "learner",
            "guardian",
            privateNote,
            Guid.NewGuid(),
            UtcNow());

        Assert.Equal(privateNote, evaluation.TeacherPrivateNote);
        Assert.Equal("learner", evaluation.LearnerFeedback);
        Assert.Equal("guardian", evaluation.GuardianVisibleFeedback);
    }

    private static EvaluationRevision CreateDraft(decimal score, decimal maxScore) =>
        EvaluationRevision.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            score,
            maxScore,
            "بازخورد دانش‌آموز",
            "بازخورد والد",
            "یادداشت خصوصی معلم",
            Guid.NewGuid(),
            UtcNow());

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
