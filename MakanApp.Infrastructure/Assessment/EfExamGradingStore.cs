using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfExamGradingStore(MakanDbContext dbContext) : IExamGradingStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamGradingTransaction(transaction);
    }

    public async Task<ExamGradeAggregateRecord?> GetAggregateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == attemptId,
                cancellationToken);
        return attempt is null
            ? null
            : await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
    }

    public async Task<ExamGradeAggregateRecord?> GetAggregateForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.ExamAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        _ = await dbContext.ExamGradeRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamGradeRevisions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        _ = await dbContext.ExamQuestionGrades
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamQuestionGrades] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        _ = await dbContext.ExamGradeReleases
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[ExamGradeReleases] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        return await LoadAggregateAsync(attempt, asNoTracking: false, cancellationToken);
    }

    public Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueForManagerAsync(
        Guid organizationId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(organizationId, null, examId, query, cancellationToken);

    public Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueForTeacherAsync(
        Guid organizationId,
        Guid membershipId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(organizationId, membershipId, examId, query, cancellationToken);

    public Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeacherAssignments.AnyAsync(
            assignment => assignment.OrganizationId == organizationId &&
                          assignment.ClassId == classId &&
                          assignment.TeacherMembershipId == membershipId &&
                          assignment.Status == TeacherAssignmentStatus.Active &&
                          assignment.EndedAtUtc == null,
            cancellationToken);

    public void Add(ExamGradeRevision revision) => dbContext.ExamGradeRevisions.Add(revision);

    public void AddRange(IEnumerable<ExamQuestionGrade> questionGrades) =>
        dbContext.ExamQuestionGrades.AddRange(questionGrades);

    public void Add(ExamGradeRelease release) => dbContext.ExamGradeReleases.Add(release);

    public void SetOriginalRowVersion(ExamGradeRevision revision, byte[] rowVersion) =>
        dbContext.Entry(revision).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "نمره آزمون هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeReleaseConflict,
                "نمره یا انتشار هم‌زمان دیگری برای این آزمون ثبت شده است.");
        }
    }

    private async Task<IReadOnlyCollection<ExamGradingQueueRecord>> GetQueueAsync(
        Guid organizationId,
        Guid? teacherMembershipId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken)
    {
        var attempts = await (
            from attempt in dbContext.ExamAttempts
            join exam in dbContext.Exams
                on new { attempt.OrganizationId, attempt.ExamId }
                equals new { exam.OrganizationId, ExamId = exam.Id }
            where attempt.OrganizationId == organizationId &&
                  attempt.ExamId == examId &&
                  attempt.Status == ExamAttemptStatus.Finalized &&
                  (!query.ClassId.HasValue || exam.ClassId == query.ClassId.Value) &&
                  (!teacherMembershipId.HasValue || dbContext.TeacherAssignments.Any(assignment =>
                      assignment.OrganizationId == organizationId &&
                      assignment.ClassId == exam.ClassId &&
                      assignment.TeacherMembershipId == teacherMembershipId.Value &&
                      assignment.Status == TeacherAssignmentStatus.Active &&
                      assignment.EndedAtUtc == null))
            orderby attempt.FinalizedAtUtc, attempt.Id
            select attempt)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        var results = new List<ExamGradingQueueRecord>(attempts.Length);
        foreach (var attempt in attempts)
        {
            var aggregate = await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
            results.Add(new ExamGradingQueueRecord(
                aggregate.Source,
                aggregate.LatestRevision,
                aggregate.CurrentReleasedRevision));
        }

        return results;
    }

    private async Task<ExamGradeAggregateRecord> LoadAggregateAsync(
        ExamAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var source = await LoadSourceAsync(attempt, asNoTracking, cancellationToken);
        IQueryable<ExamGradeRevision> revisionsQuery = dbContext.ExamGradeRevisions.Where(
            revision => revision.OrganizationId == attempt.OrganizationId &&
                        revision.ExamAttemptId == attempt.Id);
        IQueryable<ExamQuestionGrade> gradesQuery = dbContext.ExamQuestionGrades.Where(
            grade => grade.OrganizationId == attempt.OrganizationId &&
                     grade.ExamAttemptId == attempt.Id);
        IQueryable<ExamGradeRelease> releasesQuery = dbContext.ExamGradeReleases.Where(
            release => release.OrganizationId == attempt.OrganizationId &&
                       release.ExamAttemptId == attempt.Id);
        if (asNoTracking)
        {
            revisionsQuery = revisionsQuery.AsNoTracking();
            gradesQuery = gradesQuery.AsNoTracking();
            releasesQuery = releasesQuery.AsNoTracking();
        }

        var revisions = await revisionsQuery
            .OrderBy(revision => revision.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var latestRevision = revisions.LastOrDefault();
        var currentReleased = revisions.SingleOrDefault(
            revision => revision.Status == ExamGradeRevisionStatus.Released);
        var releases = await releasesQuery
            .OrderBy(release => release.ReleasedAtUtc)
            .ToArrayAsync(cancellationToken);
        var currentRelease = currentReleased is null
            ? null
            : releases.SingleOrDefault(release =>
                release.ExamGradeRevisionId == currentReleased.Id);
        var latestGrades = latestRevision is null
            ? []
            : await gradesQuery
                .Where(grade => grade.ExamGradeRevisionId == latestRevision.Id)
                .ToArrayAsync(cancellationToken);
        return new ExamGradeAggregateRecord(
            source,
            revisions,
            latestRevision,
            currentReleased,
            currentRelease,
            latestGrades,
            releases);
    }

    private async Task<ExamGradingSourceRecord> LoadSourceAsync(
        ExamAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Exam> examQuery = dbContext.Exams.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.ExamId);
        IQueryable<ExamVersion> versionQuery = dbContext.ExamVersions.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.ExamVersionId);
        IQueryable<Enrollment> enrollmentQuery = dbContext.Enrollments.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.EnrollmentId);
        IQueryable<ExamAttemptQuestion> attemptQuestionsQuery = dbContext.ExamAttemptQuestions.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.ExamAttemptId == attempt.Id);
        if (asNoTracking)
        {
            examQuery = examQuery.AsNoTracking();
            versionQuery = versionQuery.AsNoTracking();
            enrollmentQuery = enrollmentQuery.AsNoTracking();
            attemptQuestionsQuery = attemptQuestionsQuery.AsNoTracking();
        }

        var exam = await examQuery.SingleAsync(cancellationToken);
        var version = await versionQuery.SingleAsync(cancellationToken);
        var enrollment = await enrollmentQuery.SingleAsync(cancellationToken);
        var attemptQuestions = await attemptQuestionsQuery
            .OrderBy(question => question.DisplayOrder)
            .ToArrayAsync(cancellationToken);
        var questionIds = attemptQuestions.Select(question => question.QuestionVersionId).ToArray();
        var questions = await dbContext.QuestionVersions
            .AsNoTracking()
            .Include(question => question.Options)
            .Where(question => question.OrganizationId == attempt.OrganizationId &&
                               questionIds.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, cancellationToken);
        var finalAnswers = await dbContext.ExamFinalAnswers
            .AsNoTracking()
            .Where(answer => answer.OrganizationId == attempt.OrganizationId &&
                             answer.ExamAttemptId == attempt.Id)
            .ToDictionaryAsync(answer => answer.ExamAttemptQuestionId, cancellationToken);
        var revisionIds = finalAnswers.Values.Select(answer => answer.AnswerRevisionId).ToArray();
        var finalRevisions = await dbContext.AnswerRevisions
            .AsNoTracking()
            .Where(revision => revision.OrganizationId == attempt.OrganizationId &&
                               revision.ExamAttemptId == attempt.Id &&
                               revisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        var student = await (
            from organizationPerson in dbContext.OrganizationPersons
            join person in dbContext.Persons on organizationPerson.PersonId equals person.Id
            join user in dbContext.Users on person.Id equals user.PersonId
            where organizationPerson.OrganizationId == attempt.OrganizationId &&
                  organizationPerson.Id == enrollment.LearnerOrganizationPersonId
            select new
            {
                UserId = user.Id,
                DisplayName = person.DisplayName ?? person.FirstName + " " + person.LastName
            })
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        var gradingQuestions = attemptQuestions.Select(attemptQuestion =>
        {
            finalAnswers.TryGetValue(attemptQuestion.Id, out var finalAnswer);
            AnswerRevision? finalRevision = null;
            if (finalAnswer is not null)
            {
                finalRevision = finalRevisions[finalAnswer.AnswerRevisionId];
            }

            return new ExamGradingQuestionSourceRecord(
                attemptQuestion,
                questions[attemptQuestion.QuestionVersionId],
                finalAnswer,
                finalRevision);
        }).ToArray();
        return new ExamGradingSourceRecord(
            attempt,
            exam,
            version,
            enrollment,
            student.UserId,
            student.DisplayName,
            gradingQuestions);
    }

    private sealed class EfExamGradingTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
