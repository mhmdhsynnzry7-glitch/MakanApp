using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfExamFinalizationStore(
    MakanDbContext dbContext,
    ILogger<EfExamFinalizationStore> logger) : IExamFinalizationStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamFinalizationTransaction(transaction);
    }

    public async Task<ExamAttempt?> GetOwnedAttemptForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null ||
            !await IsOwnedAsync(organizationId, attemptId, userId, cancellationToken))
        {
            return null;
        }

        return attempt;
    }

    public async Task<bool> IsSessionActiveForUpdateAsync(
        Guid sessionId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.UserSessions
            .FromSqlInterpolated(
                $"SELECT * FROM [identity].[UserSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {sessionId} AND [UserId] = {userId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return session?.IsActive(nowUtc) == true;
    }

    public async Task<IReadOnlyCollection<ExamFinalizationQuestionRecord>> GetQuestionsForFinalizationAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var questions = await dbContext.ExamAttemptQuestions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttemptQuestions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamAttemptId] = {attemptId}")
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var revisionIds = questions
            .Where(question => question.CurrentAnswerRevisionId.HasValue)
            .Select(question => question.CurrentAnswerRevisionId!.Value)
            .ToArray();
        var revisions = await dbContext.AnswerRevisions
            .AsNoTracking()
            .Where(revision => revision.OrganizationId == organizationId &&
                               revision.ExamAttemptId == attemptId &&
                               revisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        return questions
            .Select(question => new ExamFinalizationQuestionRecord(
                question,
                question.CurrentAnswerRevisionId.HasValue
                    ? revisions[question.CurrentAnswerRevisionId.Value]
                    : null))
            .ToArray();
    }

    public async Task<ExamFinalizationRecord?> GetOwnedFinalizationAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsOwnedAsync(organizationId, attemptId, userId, cancellationToken))
        {
            return null;
        }

        var attempt = await dbContext.ExamAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == attemptId,
                cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var questions = await dbContext.ExamAttemptQuestions
            .AsNoTracking()
            .Where(question => question.OrganizationId == organizationId &&
                               question.ExamAttemptId == attemptId)
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var finalAnswers = await (
            from finalAnswer in dbContext.ExamFinalAnswers.AsNoTracking()
            join revision in dbContext.AnswerRevisions.AsNoTracking()
                on new
                {
                    finalAnswer.OrganizationId,
                    finalAnswer.ExamAttemptId,
                    finalAnswer.ExamAttemptQuestionId,
                    Id = finalAnswer.AnswerRevisionId
                }
                equals new
                {
                    revision.OrganizationId,
                    revision.ExamAttemptId,
                    revision.ExamAttemptQuestionId,
                    Id = revision.Id
                }
            where finalAnswer.OrganizationId == organizationId &&
                  finalAnswer.ExamAttemptId == attemptId
            select new ExamFinalAnswerRecord(finalAnswer, revision))
            .ToArrayAsync(cancellationToken);
        return new ExamFinalizationRecord(attempt, questions, finalAnswers);
    }

    public void AddRange(IEnumerable<ExamFinalAnswer> finalAnswers) =>
        dbContext.ExamFinalAnswers.AddRange(finalAnswers);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Exam finalization concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "وضعیت تلاش آزمون هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            logger.LogWarning(
                "Exam finalization uniqueness conflict; no answer payload was logged.");
            throw new AssessmentException(
                AssessmentErrorCodes.ExamFinalizeConflict,
                "نهایی‌سازی هم‌زمان آزمون با تداخل مواجه شد.");
        }
    }

    private Task<bool> IsOwnedAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from attempt in dbContext.ExamAttempts
            join enrollment in dbContext.Enrollments
                on new { attempt.OrganizationId, attempt.ClassId, Id = attempt.EnrollmentId }
                equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
            join organizationPerson in dbContext.OrganizationPersons
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where attempt.OrganizationId == organizationId &&
                  attempt.Id == attemptId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null &&
                  user.Id == userId
            select attempt.Id)
            .AnyAsync(cancellationToken);

    private sealed class EfExamFinalizationTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
