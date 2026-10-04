using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<SubmissionDatabaseRecord> GetSubmissionAttemptAsync(Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.SubmissionAttempts
            .AsNoTracking()
            .Where(attempt => attempt.Id == attemptId)
            .Select(attempt => new SubmissionDatabaseRecord(
                attempt.Id,
                attempt.OrganizationId,
                attempt.AssignmentId,
                attempt.AssignmentVersionId,
                attempt.AssignmentRecipientId,
                attempt.EnrollmentId,
                attempt.AttemptNumber,
                attempt.Status,
                attempt.AnswerText,
                attempt.CreatedAtUtc,
                attempt.LastSavedAtUtc,
                attempt.SubmittedAtUtc,
                attempt.IsLate))
            .SingleAsync();
    }

    public async Task<int> CountSubmissionAttemptsAsync(Guid assignmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.SubmissionAttempts.CountAsync(
            attempt => attempt.AssignmentId == assignmentId);
    }

    public async Task<int> CountSubmittedAttemptsAsync(Guid assignmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.SubmissionAttempts.CountAsync(
            attempt => attempt.AssignmentId == assignmentId &&
                       attempt.Status == SubmissionAttemptStatus.Submitted);
    }

    public async Task<int> CountSubmissionAttachmentsAsync(Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.SubmissionAttachments.CountAsync(
            attachment => attachment.SubmissionAttemptId == attemptId);
    }

    public async Task SetAssignmentDeadlineAsync(
        Guid assignmentVersionId,
        DateTime dueAtUtc,
        bool allowLateSubmission)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var createdAtUtc = dueAtUtc.AddDays(-1);
        var publishedAtUtc = dueAtUtc.AddHours(-1);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [assessment].[AssignmentVersions]
            SET [CreatedAtUtc] = {createdAtUtc},
                [PublishedAtUtc] = {publishedAtUtc},
                [DueAtUtc] = {dueAtUtc},
                [AllowLateSubmission] = {allowLateSubmission}
            WHERE [Id] = {assignmentVersionId}
            """);
    }

    public async Task SetFileAssetStatusAsync(Guid fileAssetId, FileAssetStatus status)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var fileAsset = await dbContext.FileAssets.SingleAsync(file => file.Id == fileAssetId);
        var nowUtc = DateTime.UtcNow;
        switch (status)
        {
            case FileAssetStatus.Rejected:
                fileAsset.MarkRejected(nowUtc);
                break;
            case FileAssetStatus.Deleted:
                fileAsset.MarkDeleted(nowUtc);
                break;
            case FileAssetStatus.Pending:
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE [storage].[FileAssets]
                    SET [Status] = 1,
                        [SizeBytes] = 0,
                        [Sha256Hash] = NULL,
                        [CompletedAtUtc] = NULL
                    WHERE [Id] = {fileAssetId}
                    """);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> DuplicateDraftIsRejectedBySqlServerAsync(Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var existing = await dbContext.SubmissionAttempts
            .AsNoTracking()
            .SingleAsync(attempt => attempt.Id == attemptId);
        dbContext.SubmissionAttempts.Add(SubmissionAttempt.CreateDraft(
            existing.OrganizationId,
            existing.AssignmentId,
            existing.AssignmentVersionId,
            existing.AssignmentRecipientId,
            existing.EnrollmentId,
            existing.AttemptNumber + 1,
            DateTime.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }
}

public sealed record SubmissionDatabaseRecord(
    Guid Id,
    Guid OrganizationId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid AssignmentRecipientId,
    Guid EnrollmentId,
    int AttemptNumber,
    SubmissionAttemptStatus Status,
    string? AnswerText,
    DateTime CreatedAtUtc,
    DateTime? LastSavedAtUtc,
    DateTime? SubmittedAtUtc,
    bool IsLate);
