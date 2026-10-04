using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class AssignmentModelTests
{
    [Fact]
    public void NewAssignmentStartsAsDraftWithFirstVersion()
    {
        var assignment = Assignment.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UtcNow());

        Assert.Equal(AssignmentStatus.Draft, assignment.Status);
        Assert.Equal(1, assignment.CurrentVersionNumber);
        Assert.Null(assignment.PublishedAtUtc);
    }

    [Fact]
    public void DraftVersionNormalizesContent()
    {
        var version = CreateVersion("  تمرین اول  ", "  توضیح تمرین  ");

        Assert.Equal("تمرین اول", version.Title);
        Assert.Equal("توضیح تمرین", version.Description);
        Assert.False(version.IsPublished);
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

    [Fact]
    public void VersionRejectsNonUtcDueDate()
    {
        var dueAt = DateTime.SpecifyKind(DateTime.Now.AddDays(2), DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => CreateVersion(dueAtUtc: dueAt));
    }

    [Fact]
    public void VersionRejectsPastDueDate()
    {
        Assert.Throws<ArgumentException>(() => CreateVersion(dueAtUtc: UtcNow().AddMinutes(-1)));
    }

    [Fact]
    public void PublishedVersionCannotBeEdited()
    {
        var version = CreateVersion();
        version.Publish(UtcNow());

        Assert.Throws<InvalidOperationException>(() => version.UpdateDraft(
            "عنوان جدید",
            "توضیح جدید",
            UtcNow().AddDays(3),
            true,
            2,
            25m,
            UtcNow()));
    }

    [Fact]
    public void AssignmentCannotBePublishedTwice()
    {
        var assignment = Assignment.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UtcNow());
        assignment.Publish(UtcNow());

        Assert.Throws<InvalidOperationException>(() => assignment.Publish(UtcNow()));
    }

    [Fact]
    public void DraftIsNotRecipientVisibleButPublishedAssignmentIsVisible()
    {
        var assignment = Assignment.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UtcNow());

        Assert.False(assignment.IsRecipientVisible);

        assignment.Publish(UtcNow());

        Assert.True(assignment.IsRecipientVisible);
    }

    [Fact]
    public void RecipientCapturesEnrollmentIdentity()
    {
        var enrollmentId = Guid.NewGuid();
        var recipient = AssignmentRecipient.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            enrollmentId,
            UtcNow());

        Assert.Equal(enrollmentId, recipient.EnrollmentId);
    }

    private static AssignmentVersion CreateVersion(
        string title = "تمرین",
        string description = "توضیح",
        DateTime? dueAtUtc = null,
        int maxAttempts = 1,
        decimal maxScore = 20m)
    {
        var now = UtcNow();
        return AssignmentVersion.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            title,
            description,
            dueAtUtc ?? now.AddDays(1),
            false,
            maxAttempts,
            maxScore,
            Guid.NewGuid(),
            now);
    }

    private static DateTime UtcNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
