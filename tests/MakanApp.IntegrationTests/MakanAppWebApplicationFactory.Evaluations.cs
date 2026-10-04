using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<EvaluationDatabaseRecord[]> GetEvaluationRevisionsAsync(Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.EvaluationRevisions
            .AsNoTracking()
            .Where(evaluation => evaluation.SubmissionAttemptId == attemptId)
            .OrderBy(evaluation => evaluation.RevisionNumber)
            .Select(evaluation => new EvaluationDatabaseRecord(
                evaluation.Id,
                evaluation.RevisionNumber,
                evaluation.Score,
                evaluation.LearnerFeedback,
                evaluation.GuardianVisibleFeedback,
                evaluation.TeacherPrivateNote,
                evaluation.Status,
                evaluation.SupersedesEvaluationRevisionId,
                evaluation.CorrectionReason))
            .ToArrayAsync();
    }

    public async Task<int> CountGradeReleasesAsync(Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.GradeReleases.CountAsync(
            release => release.SubmissionAttemptId == attemptId);
    }

    public async Task<decimal> GetAssignmentMaximumScoreAsync(Guid assignmentVersionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AssignmentVersions
            .Where(version => version.Id == assignmentVersionId)
            .Select(version => version.MaxScore)
            .SingleAsync();
    }

    public async Task<bool> EvaluationRowVersionDetectsStaleWriteAsync(Guid evaluationId)
    {
        await using var firstScope = Services.CreateAsyncScope();
        await using var secondScope = Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var first = await firstDbContext.EvaluationRevisions.SingleAsync(item => item.Id == evaluationId);
        var second = await secondDbContext.EvaluationRevisions.SingleAsync(item => item.Id == evaluationId);
        var maxScore = await firstDbContext.AssignmentVersions
            .Where(version => firstDbContext.SubmissionAttempts
                .Where(attempt => attempt.Id == first.SubmissionAttemptId)
                .Select(attempt => attempt.AssignmentVersionId)
                .Contains(version.Id))
            .Select(version => version.MaxScore)
            .SingleAsync();

        first.UpdateDraft(
            first.Score,
            maxScore,
            "first write",
            first.GuardianVisibleFeedback,
            first.TeacherPrivateNote,
            DateTime.UtcNow);
        await firstDbContext.SaveChangesAsync();
        second.UpdateDraft(
            second.Score,
            maxScore,
            "stale write",
            second.GuardianVisibleFeedback,
            second.TeacherPrivateNote,
            DateTime.UtcNow);
        try
        {
            await secondDbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            return true;
        }
    }
}

public sealed record EvaluationDatabaseRecord(
    Guid Id,
    int RevisionNumber,
    decimal Score,
    string? LearnerFeedback,
    string? GuardianVisibleFeedback,
    string? TeacherPrivateNote,
    EvaluationRevisionStatus Status,
    Guid? SupersedesEvaluationRevisionId,
    string? CorrectionReason);
