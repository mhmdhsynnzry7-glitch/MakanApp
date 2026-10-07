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

public sealed class EfExamAttemptStore(
    MakanDbContext dbContext,
    ILogger<EfExamAttemptStore> logger) : IExamAttemptStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamAttemptTransaction(transaction);
    }

    public async Task<ExamStartContextRecord?> GetStartContextForUpdateAsync(
        Guid organizationId,
        Guid examId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[Exams] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {examId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (exam is null)
        {
            return null;
        }

        var enrollmentId = await (
            from activeEnrollment in dbContext.Enrollments
            join organizationPerson in dbContext.OrganizationPersons
                on new { activeEnrollment.OrganizationId, Id = activeEnrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where activeEnrollment.OrganizationId == organizationId &&
                  activeEnrollment.ClassId == exam.ClassId &&
                  activeEnrollment.Status == EnrollmentStatus.Active &&
                  activeEnrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null &&
                  user.Id == userId
            select (Guid?)activeEnrollment.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!enrollmentId.HasValue)
        {
            return new ExamStartContextRecord(exam, null, null, []);
        }

        var enrollment = await dbContext.Enrollments
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ClassId] = {exam.ClassId} AND [Id] = {enrollmentId.Value}")
            .SingleAsync(cancellationToken);
        if (!exam.LatestPublishedVersionNumber.HasValue)
        {
            return new ExamStartContextRecord(exam, enrollment, null, []);
        }

        var version = await dbContext.ExamVersions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamId] = {examId} AND [VersionNumber] = {exam.LatestPublishedVersionNumber.Value}")
            .SingleOrDefaultAsync(cancellationToken);
        if (version is null)
        {
            return new ExamStartContextRecord(exam, enrollment, null, []);
        }

        var questions = await dbContext.QuestionVersions
            .Include(question => question.Options)
            .Where(question => question.OrganizationId == organizationId &&
                               question.ExamVersionId == version.Id)
            .OrderBy(question => question.Order)
            .ToArrayAsync(cancellationToken);
        return new ExamStartContextRecord(exam, enrollment, version, questions);
    }

    public async Task<IReadOnlyCollection<ExamAttempt>> GetAttemptsForUpdateAsync(
        Guid organizationId,
        Guid examId,
        Guid enrollmentId,
        CancellationToken cancellationToken) =>
        (await dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamId] = {examId} AND [EnrollmentId] = {enrollmentId}")
            .ToArrayAsync(cancellationToken))
        .OrderBy(attempt => attempt.AttemptNumber)
        .ToArray();

    public Task<ExamAttempt?> GetByClientOperationForUpdateAsync(
        Guid organizationId,
        Guid enrollmentId,
        Guid clientOperationId,
        CancellationToken cancellationToken) =>
        dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [EnrollmentId] = {enrollmentId} AND [ClientOperationId] = {clientOperationId}")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ExamAttemptRecord?> GetByIdAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == attemptId,
                cancellationToken);
        return attempt is null
            ? null
            : await BuildRecordAsync(attempt, cancellationToken);
    }

    public async Task<ExamAttemptRecord?> GetOwnedByIdAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var ownedAttemptId = await CreateOwnedAttemptQuery(organizationId, userId)
            .Where(attempt => attempt.Id == attemptId)
            .Select(attempt => (Guid?)attempt.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return ownedAttemptId.HasValue
            ? await GetByIdAsync(organizationId, ownedAttemptId.Value, cancellationToken)
            : null;
    }

    public async Task<ExamAttemptRecord?> GetOwnedActiveAsync(
        Guid organizationId,
        Guid examId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var attemptId = await CreateOwnedAttemptQuery(organizationId, userId)
            .Where(attempt => attempt.ExamId == examId &&
                              attempt.Status == ExamAttemptStatus.InProgress &&
                              attempt.EffectiveDeadlineUtc > nowUtc)
            .Select(attempt => (Guid?)attempt.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return attemptId.HasValue
            ? await GetByIdAsync(organizationId, attemptId.Value, cancellationToken)
            : null;
    }

    public async Task<IReadOnlyCollection<ExamAttemptRecord>> GetOwnedAttemptsAsync(
        Guid organizationId,
        Guid examId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var attemptIds = await CreateOwnedAttemptQuery(organizationId, userId)
            .Where(attempt => attempt.ExamId == examId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .Select(attempt => attempt.Id)
            .ToArrayAsync(cancellationToken);
        var records = new List<ExamAttemptRecord>(attemptIds.Length);
        foreach (var attemptId in attemptIds)
        {
            var record = await GetByIdAsync(organizationId, attemptId, cancellationToken);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    public void Add(ExamAttempt attempt) => dbContext.ExamAttempts.Add(attempt);

    public void AddRange(IEnumerable<ExamAttemptQuestion> questions) =>
        dbContext.ExamAttemptQuestions.AddRange(questions);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Exam attempt optimistic concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "تلاش آزمون هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            logger.LogWarning(exception, "Exam attempt database uniqueness conflict.");
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "شروع آزمون با درخواست هم‌زمان تداخل داشت؛ دوباره تلاش کنید.");
        }
    }

    private IQueryable<ExamAttempt> CreateOwnedAttemptQuery(Guid organizationId, Guid userId) =>
        from attempt in dbContext.ExamAttempts.AsNoTracking()
        join enrollment in dbContext.Enrollments
            on new { attempt.OrganizationId, attempt.ClassId, Id = attempt.EnrollmentId }
            equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
        join organizationPerson in dbContext.OrganizationPersons
            on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
            equals new { organizationPerson.OrganizationId, organizationPerson.Id }
        join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
        where attempt.OrganizationId == organizationId &&
              enrollment.Status == EnrollmentStatus.Active &&
              enrollment.EndedAtUtc == null &&
              organizationPerson.Status == OrganizationPersonStatus.Active &&
              organizationPerson.EndedAtUtc == null &&
              user.Id == userId
        select attempt;

    private async Task<ExamAttemptRecord> BuildRecordAsync(
        ExamAttempt attempt,
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.ExamAttemptQuestions
            .AsNoTracking()
            .Where(mapping => mapping.OrganizationId == attempt.OrganizationId &&
                              mapping.ExamAttemptId == attempt.Id)
            .OrderBy(mapping => mapping.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var questionIds = mappings.Select(mapping => mapping.QuestionVersionId).ToArray();
        var questions = await dbContext.QuestionVersions
            .AsNoTracking()
            .Include(question => question.Options)
            .Where(question => question.OrganizationId == attempt.OrganizationId &&
                               questionIds.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, cancellationToken);
        return new ExamAttemptRecord(
            attempt,
            mappings.Select(mapping => new FrozenExamQuestionRecord(
                mapping,
                questions[mapping.QuestionVersionId]))
                .ToArray());
    }

    private sealed class EfExamAttemptTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
