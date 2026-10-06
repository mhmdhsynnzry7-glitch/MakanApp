using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class ExamModelTests
{
    [Fact]
    public void VersionRejectsInvalidWindow()
    {
        var now = UtcNow();

        Assert.Throws<ArgumentException>(() => CreateVersion(
            availableFromUtc: now.AddHours(2),
            availableUntilUtc: now.AddHours(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VersionRejectsNonPositiveDuration(int durationMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateVersion(durationMinutes: durationMinutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VersionRejectsNonPositiveMaxAttempts(int maxAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateVersion(maxAttempts: maxAttempts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VersionRejectsNonPositiveMaxScore(int maxScore)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateVersion(maxScore: maxScore));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuestionRejectsNonPositiveScore(int score)
    {
        var version = CreateVersion();

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescriptiveQuestion(version, score));
    }

    [Fact]
    public void ObjectiveQuestionRequiresExactlyOneCorrectOption()
    {
        var version = CreateVersion();

        Assert.Throws<ExamAnswerKeyInvalidException>(() => QuestionVersion.CreateDraft(
            version.OrganizationId,
            version.Id,
            1,
            ExamQuestionType.ObjectiveSingleChoice,
            "۲ + ۲؟",
            20m,
            [
                new ExamQuestionOptionDefinition(1, "۳", false),
                new ExamQuestionOptionDefinition(2, "۴", false)
            ],
            UtcNow()));
    }

    [Fact]
    public void ObjectiveQuestionRequiresValidDistinctOptions()
    {
        var version = CreateVersion();

        Assert.Throws<ExamAnswerKeyInvalidException>(() => QuestionVersion.CreateDraft(
            version.OrganizationId,
            version.Id,
            1,
            ExamQuestionType.ObjectiveSingleChoice,
            "۲ + ۲؟",
            20m,
            [
                new ExamQuestionOptionDefinition(1, "۴", true),
                new ExamQuestionOptionDefinition(1, "سه", false)
            ],
            UtcNow()));
    }

    [Fact]
    public void DescriptiveQuestionDoesNotAcceptAnswerKeyOptions()
    {
        var version = CreateVersion();

        Assert.Throws<ExamAnswerKeyInvalidException>(() => QuestionVersion.CreateDraft(
            version.OrganizationId,
            version.Id,
            1,
            ExamQuestionType.Descriptive,
            "پاسخ را توضیح دهید.",
            20m,
            [new ExamQuestionOptionDefinition(1, "پاسخ", true)],
            UtcNow()));
    }

    [Fact]
    public void PublicationRequiresAtLeastOneQuestion()
    {
        var version = CreateVersion();

        Assert.Throws<ExamQuestionRequiredException>(() => version.Publish([], UtcNow()));
    }

    [Fact]
    public void PublicationRejectsQuestionScoreTotalMismatch()
    {
        var version = CreateVersion(maxScore: 20m);
        var question = CreateDescriptiveQuestion(version, 10m);

        Assert.Throws<ExamScoreTotalInvalidException>(() => version.Publish([question], UtcNow()));
    }

    [Fact]
    public void DraftIsEditableButPublishedVersionIsLocked()
    {
        var version = CreateVersion();
        var question = CreateDescriptiveQuestion(version, 20m);
        version.UpdateDraft(
            "عنوان جدید",
            null,
            UtcNow().AddMinutes(-1),
            UtcNow().AddHours(2),
            30,
            1,
            20m,
            ExamRandomizationPolicy.None,
            UtcNow());
        version.Publish([question], UtcNow());

        Assert.Throws<InvalidOperationException>(() => version.UpdateDraft(
            "ویرایش غیرمجاز",
            null,
            UtcNow(),
            UtcNow().AddHours(2),
            30,
            1,
            20m,
            ExamRandomizationPolicy.None,
            UtcNow()));
    }

    [Fact]
    public void NextVersionCopyDoesNotMutatePublishedQuestionHistory()
    {
        var exam = Exam.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UtcNow());
        var firstVersion = CreateVersion(exam.OrganizationId, exam.Id, maxScore: 20m);
        var firstQuestion = QuestionVersion.CreateDraft(
            exam.OrganizationId,
            firstVersion.Id,
            1,
            ExamQuestionType.ObjectiveSingleChoice,
            "2 + 2?",
            20m,
            [
                new ExamQuestionOptionDefinition(1, "3", false),
                new ExamQuestionOptionDefinition(2, "4", true)
            ],
            UtcNow());
        firstVersion.Publish([firstQuestion], UtcNow());
        exam.PublishCurrentVersion(1);
        var nextNumber = exam.StartNextDraft(1);
        var nextVersion = firstVersion.CreateNextDraft(nextNumber, Guid.NewGuid(), UtcNow());
        var copiedQuestion = firstQuestion.CopyTo(nextVersion.Id, UtcNow());

        copiedQuestion.UpdateDraft(
            nextVersion,
            1,
            ExamQuestionType.ObjectiveSingleChoice,
            "3 + 3?",
            20m,
            [
                new ExamQuestionOptionDefinition(1, "5", false),
                new ExamQuestionOptionDefinition(2, "6", true)
            ],
            UtcNow());

        Assert.Equal("2 + 2?", firstQuestion.Prompt);
        Assert.Equal("4", Assert.Single(firstQuestion.Options, option => option.IsCorrect).Text);
        Assert.Equal("3 + 3?", copiedQuestion.Prompt);
    }

    private static ExamVersion CreateVersion(
        Guid? organizationId = null,
        Guid? examId = null,
        DateTime? availableFromUtc = null,
        DateTime? availableUntilUtc = null,
        int durationMinutes = 45,
        int maxAttempts = 1,
        decimal maxScore = 20m)
    {
        var now = UtcNow();
        return ExamVersion.CreateDraft(
            organizationId ?? Guid.NewGuid(),
            examId ?? Guid.NewGuid(),
            1,
            "آزمون علوم",
            "توضیحات آزمون",
            availableFromUtc ?? now.AddMinutes(-5),
            availableUntilUtc ?? now.AddHours(2),
            durationMinutes,
            maxAttempts,
            maxScore,
            ExamRandomizationPolicy.None,
            Guid.NewGuid(),
            now);
    }

    private static QuestionVersion CreateDescriptiveQuestion(ExamVersion version, decimal score) =>
        QuestionVersion.CreateDraft(
            version.OrganizationId,
            version.Id,
            1,
            ExamQuestionType.Descriptive,
            "پاسخ را توضیح دهید.",
            score,
            null,
            UtcNow());

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
