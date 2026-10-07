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

public sealed class EfExamAnswerStore(
    MakanDbContext dbContext,
    ILogger<EfExamAnswerStore> logger) : IExamAnswerStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamAnswerTransaction(transaction);
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

    public async Task<ExamAnswersRecord?> GetOwnedAnswersAsync(
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

        var mappings = await dbContext.ExamAttemptQuestions
            .AsNoTracking()
            .Where(question => question.OrganizationId == organizationId &&
                               question.ExamAttemptId == attemptId)
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        return new ExamAnswersRecord(
            attempt,
            await BuildQuestionRecordsAsync(mappings, cancellationToken));
    }

    public async Task<ExamAnswerQuestionRecord?> GetQuestionForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        Guid attemptQuestionId,
        CancellationToken cancellationToken)
    {
        var attemptQuestion = await dbContext.ExamAttemptQuestions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttemptQuestions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamAttemptId] = {attemptId} AND [Id] = {attemptQuestionId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attemptQuestion is null)
        {
            return null;
        }

        var question = await dbContext.QuestionVersions
            .AsNoTracking()
            .Include(item => item.Options)
            .SingleAsync(
                item => item.OrganizationId == organizationId &&
                        item.Id == attemptQuestion.QuestionVersionId,
                cancellationToken);
        AnswerRevision? currentRevision = null;
        if (attemptQuestion.CurrentAnswerRevisionId.HasValue)
        {
            currentRevision = await dbContext.AnswerRevisions
                .AsNoTracking()
                .SingleAsync(
                    revision => revision.OrganizationId == organizationId &&
                                revision.Id == attemptQuestion.CurrentAnswerRevisionId.Value,
                    cancellationToken);
        }

        return new ExamAnswerQuestionRecord(attemptQuestion, question, currentRevision);
    }

    public Task<AnswerRevision?> GetByClientOperationForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        Guid clientOperationId,
        CancellationToken cancellationToken) =>
        dbContext.AnswerRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AnswerRevisions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamAttemptId] = {attemptId} AND [ClientOperationId] = {clientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(AnswerRevision revision) => dbContext.AnswerRevisions.Add(revision);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Exam answer concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "پاسخ یا وضعیت تلاش هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            logger.LogWarning(
                "Exam answer uniqueness conflict for attempt/question metadata; no answer payload was logged.");
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "ذخیره هم‌زمان پاسخ با تداخل مواجه شد؛ وضعیت جاری را دوباره دریافت کنید.");
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

    private async Task<IReadOnlyCollection<ExamAnswerQuestionRecord>> BuildQuestionRecordsAsync(
        IReadOnlyCollection<ExamAttemptQuestion> mappings,
        CancellationToken cancellationToken)
    {
        var questionIds = mappings.Select(mapping => mapping.QuestionVersionId).ToArray();
        var currentRevisionIds = mappings
            .Where(mapping => mapping.CurrentAnswerRevisionId.HasValue)
            .Select(mapping => mapping.CurrentAnswerRevisionId!.Value)
            .ToArray();
        var questions = await dbContext.QuestionVersions
            .AsNoTracking()
            .Where(question => questionIds.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, cancellationToken);
        var revisions = await dbContext.AnswerRevisions
            .AsNoTracking()
            .Where(revision => currentRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        return mappings.Select(mapping => new ExamAnswerQuestionRecord(
            mapping,
            questions[mapping.QuestionVersionId],
            mapping.CurrentAnswerRevisionId.HasValue
                ? revisions[mapping.CurrentAnswerRevisionId.Value]
                : null))
            .ToArray();
    }

    private sealed class EfExamAnswerTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
