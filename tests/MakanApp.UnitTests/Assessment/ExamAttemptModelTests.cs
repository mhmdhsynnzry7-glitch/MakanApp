using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class ExamAttemptModelTests
{
    [Fact]
    public void StartBeforeAvailableFromIsRejected()
    {
        var nowUtc = UtcNow();

        Assert.Throws<ExamNotAvailableYetException>(() => Start(
            startedAtUtc: nowUtc,
            availableFromUtc: nowUtc.AddMinutes(1)));
    }

    [Fact]
    public void StartAtOrAfterAvailableUntilIsRejected()
    {
        var nowUtc = UtcNow();

        Assert.Throws<ExamWindowClosedException>(() => Start(
            startedAtUtc: nowUtc,
            availableUntilUtc: nowUtc));
    }

    [Fact]
    public void SuccessfulStartUsesServerAllocatedAttemptAndFrozenVersion()
    {
        var versionId = Guid.NewGuid();

        var attempt = Start(examVersionId: versionId, attemptNumber: 2, maxAttempts: 2);

        Assert.Equal(versionId, attempt.ExamVersionId);
        Assert.Equal(2, attempt.AttemptNumber);
        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
    }

    [Fact]
    public void DeadlineUsesDurationWhenItEndsFirst()
    {
        var nowUtc = UtcNow();

        var attempt = Start(
            startedAtUtc: nowUtc,
            availableUntilUtc: nowUtc.AddHours(2),
            durationMinutes: 45);

        Assert.Equal(nowUtc.AddMinutes(45), attempt.EffectiveDeadlineUtc);
    }

    [Fact]
    public void DeadlineIsCappedByPublishedWindow()
    {
        var nowUtc = UtcNow();

        var attempt = Start(
            startedAtUtc: nowUtc,
            availableUntilUtc: nowUtc.AddMinutes(10),
            durationMinutes: 45);

        Assert.Equal(nowUtc.AddMinutes(10), attempt.EffectiveDeadlineUtc);
    }

    [Fact]
    public void AttemptBeyondMaxAttemptsIsRejected()
    {
        Assert.Throws<ExamAttemptsExhaustedException>(() => Start(
            attemptNumber: 3,
            maxAttempts: 2));
    }

    [Fact]
    public void FrozenQuestionKeepsAttemptVersionAndDisplayOrder()
    {
        var attempt = Start();
        var questionVersionId = Guid.NewGuid();

        var mapping = ExamAttemptQuestion.Create(attempt, questionVersionId, 3);

        Assert.Equal(attempt.Id, mapping.ExamAttemptId);
        Assert.Equal(attempt.ExamVersionId, mapping.ExamVersionId);
        Assert.Equal(questionVersionId, mapping.QuestionVersionId);
        Assert.Equal(3, mapping.DisplayOrder);
    }

    [Fact]
    public void AttemptCannotExpireBeforeDeadline()
    {
        var attempt = Start();

        Assert.Throws<InvalidOperationException>(() => attempt.MarkExpired(
            attempt.EffectiveDeadlineUtc.AddTicks(-1)));
    }

    [Fact]
    public void StudentAttemptContractsContainNoAnswerKeyProperty()
    {
        var names = typeof(StudentExamAttemptDto)
            .GetProperties()
            .Concat(typeof(StudentExamAttemptQuestionDto).GetProperties())
            .Concat(typeof(StudentExamAttemptOptionDto).GetProperties())
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(names, name => name.Contains("Correct", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
    }

    private static ExamAttempt Start(
        Guid? examVersionId = null,
        int attemptNumber = 1,
        int maxAttempts = 1,
        DateTime? availableFromUtc = null,
        DateTime? availableUntilUtc = null,
        int durationMinutes = 45,
        DateTime? startedAtUtc = null)
    {
        var nowUtc = startedAtUtc ?? UtcNow();
        return ExamAttempt.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            examVersionId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            attemptNumber,
            maxAttempts,
            availableFromUtc ?? nowUtc.AddMinutes(-5),
            availableUntilUtc ?? nowUtc.AddHours(2),
            durationMinutes,
            nowUtc);
    }

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
