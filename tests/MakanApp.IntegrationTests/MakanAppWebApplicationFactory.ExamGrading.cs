using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<ExamGradePersistenceState> GetExamGradePersistenceStateAsync(Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var revisions = await dbContext.ExamGradeRevisions
            .AsNoTracking()
            .Where(revision => revision.ExamAttemptId == attemptId)
            .OrderBy(revision => revision.RevisionNumber)
            .Select(revision => new ExamGradeRevisionPersistenceState(
                revision.Id,
                revision.RevisionNumber,
                revision.TotalScore,
                revision.Status,
                revision.SupersedesExamGradeRevisionId,
                revision.CorrectionReason))
            .ToArrayAsync();
        var questionGrades = await dbContext.ExamQuestionGrades
            .AsNoTracking()
            .Where(grade => grade.ExamAttemptId == attemptId)
            .OrderBy(grade => grade.ExamGradeRevisionId)
            .ThenBy(grade => grade.ExamAttemptQuestionId)
            .Select(grade => new ExamQuestionGradePersistenceState(
                grade.ExamGradeRevisionId,
                grade.ExamAttemptQuestionId,
                grade.AwardedScore,
                grade.MaximumScore,
                grade.GradingMode,
                grade.IsReviewed))
            .ToArrayAsync();
        var releases = await dbContext.ExamGradeReleases
            .AsNoTracking()
            .Where(release => release.ExamAttemptId == attemptId)
            .OrderBy(release => release.ReleasedAtUtc)
            .Select(release => new ExamGradeReleasePersistenceState(
                release.Id,
                release.ExamGradeRevisionId,
                release.ClientOperationId,
                release.ReleasedAtUtc))
            .ToArrayAsync();
        return new ExamGradePersistenceState(revisions, questionGrades, releases);
    }

    public sealed record ExamGradePersistenceState(
        IReadOnlyCollection<ExamGradeRevisionPersistenceState> Revisions,
        IReadOnlyCollection<ExamQuestionGradePersistenceState> QuestionGrades,
        IReadOnlyCollection<ExamGradeReleasePersistenceState> Releases);

    public sealed record ExamGradeRevisionPersistenceState(
        Guid Id,
        int RevisionNumber,
        decimal TotalScore,
        ExamGradeRevisionStatus Status,
        Guid? SupersedesExamGradeRevisionId,
        string? CorrectionReason);

    public sealed record ExamQuestionGradePersistenceState(
        Guid ExamGradeRevisionId,
        Guid ExamAttemptQuestionId,
        decimal AwardedScore,
        decimal MaximumScore,
        ExamQuestionGradingMode GradingMode,
        bool IsReviewed);

    public sealed record ExamGradeReleasePersistenceState(
        Guid Id,
        Guid ExamGradeRevisionId,
        Guid ClientOperationId,
        DateTime ReleasedAtUtc);
}
