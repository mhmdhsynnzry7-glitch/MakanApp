using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class SubmissionAttemptTests
{
    [Fact]
    public void NewAttemptStartsAsDraftAndIsNotOfficiallySubmitted()
    {
        var attempt = CreateAttempt();

        Assert.Equal(SubmissionAttemptStatus.Draft, attempt.Status);
        Assert.True(attempt.IsDraft);
        Assert.Null(attempt.SubmittedAtUtc);
        Assert.False(attempt.IsLate);
    }

    [Fact]
    public void DraftSaveNormalizesAnswerWithoutSubmitting()
    {
        var attempt = CreateAttempt();
        var savedAtUtc = UtcNow().AddMinutes(1);

        attempt.SaveAnswer("  پاسخ دانش‌آموز  ", savedAtUtc);

        Assert.Equal("پاسخ دانش‌آموز", attempt.AnswerText);
        Assert.Equal(savedAtUtc, attempt.LastSavedAtUtc);
        Assert.Equal(SubmissionAttemptStatus.Draft, attempt.Status);
    }

    [Fact]
    public void EmptyAnswerIsStoredAsNull()
    {
        var attempt = CreateAttempt();

        attempt.SaveAnswer("   ", UtcNow());

        Assert.Null(attempt.AnswerText);
    }

    [Fact]
    public void OversizedAnswerIsRejected()
    {
        var attempt = CreateAttempt();

        Assert.Throws<ArgumentException>(() => attempt.SaveAnswer(
            new string('a', SubmissionAttempt.MaximumAnswerLength + 1),
            UtcNow()));
    }

    [Fact]
    public void EmptySubmissionIsRejected()
    {
        var attempt = CreateAttempt();
        var nowUtc = UtcNow();

        Assert.Throws<SubmissionEmptyException>(() => attempt.Submit(
            nowUtc,
            nowUtc.AddMinutes(1),
            false,
            false));
        Assert.Equal(SubmissionAttemptStatus.Draft, attempt.Status);
    }

    [Fact]
    public void ReadyAttachmentCanBeTheOnlySubmissionContent()
    {
        var attempt = CreateAttempt();
        var nowUtc = UtcNow();

        attempt.Submit(nowUtc, nowUtc.AddMinutes(1), false, true);

        Assert.Equal(SubmissionAttemptStatus.Submitted, attempt.Status);
    }

    [Fact]
    public void SubmissionBeforeDeadlineUsesServerTimestamp()
    {
        var attempt = CreateAttempt();
        attempt.SaveAnswer("answer", UtcNow());
        var serverAcceptedAtUtc = UtcNow().AddMinutes(2);

        attempt.Submit(
            serverAcceptedAtUtc,
            serverAcceptedAtUtc.AddMinutes(1),
            false,
            false);

        Assert.Equal(serverAcceptedAtUtc, attempt.SubmittedAtUtc);
        Assert.False(attempt.IsLate);
    }

    [Fact]
    public void LateSubmissionIsRejectedWhenPolicyDisallowsIt()
    {
        var attempt = CreateAttempt();
        attempt.SaveAnswer("answer", UtcNow());
        var serverNowUtc = UtcNow().AddMinutes(2);

        Assert.Throws<SubmissionDeadlinePassedException>(() => attempt.Submit(
            serverNowUtc,
            serverNowUtc.AddMinutes(-1),
            false,
            false));
        Assert.Equal(SubmissionAttemptStatus.Draft, attempt.Status);
    }

    [Fact]
    public void LateSubmissionIsMarkedWhenPolicyAllowsIt()
    {
        var attempt = CreateAttempt();
        attempt.SaveAnswer("answer", UtcNow());
        var serverNowUtc = UtcNow().AddMinutes(2);

        attempt.Submit(serverNowUtc, serverNowUtc.AddMinutes(-1), true, false);

        Assert.True(attempt.IsLate);
        Assert.Equal(serverNowUtc, attempt.SubmittedAtUtc);
    }

    [Fact]
    public void SubmittedAnswerIsImmutable()
    {
        var attempt = CreateSubmittedAttempt();

        Assert.Throws<InvalidOperationException>(() =>
            attempt.SaveAnswer("replacement", UtcNow().AddMinutes(3)));
    }

    [Fact]
    public void SubmittedAttachmentsAreImmutable()
    {
        var attempt = CreateSubmittedAttempt();

        Assert.Throws<InvalidOperationException>(() =>
            attempt.MarkAttachmentsChanged(UtcNow().AddMinutes(3)));
    }

    [Fact]
    public void SubmittedAttemptCannotTransitionTwice()
    {
        var attempt = CreateSubmittedAttempt();

        Assert.Throws<InvalidOperationException>(() => attempt.Submit(
            UtcNow().AddMinutes(3),
            UtcNow().AddMinutes(4),
            false,
            false));
    }

    [Fact]
    public void SubmissionAttachmentCapturesServerUtcTimestamp()
    {
        var attachedAtUtc = UtcNow();

        var attachment = SubmissionAttachment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            attachedAtUtc);

        Assert.Equal(attachedAtUtc, attachment.AttachedAtUtc);
    }

    [Fact]
    public void SubmissionAttachmentRejectsLocalTimestamp()
    {
        Assert.Throws<ArgumentException>(() => SubmissionAttachment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local)));
    }

    private static SubmissionAttempt CreateAttempt(int attemptNumber = 1) =>
        SubmissionAttempt.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            attemptNumber,
            UtcNow());

    private static SubmissionAttempt CreateSubmittedAttempt()
    {
        var attempt = CreateAttempt();
        var nowUtc = UtcNow();
        attempt.SaveAnswer("answer", nowUtc);
        attempt.Submit(nowUtc.AddMinutes(1), nowUtc.AddMinutes(2), false, false);
        return attempt;
    }

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
