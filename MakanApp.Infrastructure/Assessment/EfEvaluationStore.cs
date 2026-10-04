using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfEvaluationStore(MakanDbContext dbContext) : IEvaluationStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfEvaluationTransaction(transaction);
    }

    public async Task<EvaluationAggregateRecord?> GetAggregateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.SubmissionAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrganizationId == organizationId && item.Id == attemptId,
                cancellationToken);
        return attempt is null
            ? null
            : await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
    }

    public async Task<EvaluationAggregateRecord?> GetAggregateForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        _ = await dbContext.EvaluationRevisions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[EvaluationRevisions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [SubmissionAttemptId] = {attemptId}")
            .ToArrayAsync(cancellationToken);
        return await LoadAggregateAsync(attempt, asNoTracking: false, cancellationToken);
    }

    public Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueForManagerAsync(
        Guid organizationId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(organizationId, null, query, cancellationToken);

    public Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueForTeacherAsync(
        Guid organizationId,
        Guid membershipId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken) =>
        GetQueueAsync(organizationId, membershipId, query, cancellationToken);

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

    public void Add(EvaluationRevision evaluationRevision) =>
        dbContext.EvaluationRevisions.Add(evaluationRevision);

    public void Add(GradeRelease gradeRelease) =>
        dbContext.GradeReleases.Add(gradeRelease);

    public void SetOriginalRowVersion(EvaluationRevision evaluationRevision, byte[] rowVersion) =>
        dbContext.Entry(evaluationRevision).Property(item => item.RowVersion).OriginalValue = rowVersion;

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
                "ارزیابی هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AssessmentException(
                AssessmentErrorCodes.GradeReleaseConflict,
                "ارزیابی یا انتشار هم‌زمان دیگری ثبت شده است.");
        }
    }

    private async Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueAsync(
        Guid organizationId,
        Guid? teacherMembershipId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken)
    {
        var attemptsQuery =
            from attempt in dbContext.SubmissionAttempts
            join assignment in dbContext.Assignments
                on new { attempt.OrganizationId, Id = attempt.AssignmentId }
                equals new { assignment.OrganizationId, assignment.Id }
            where attempt.OrganizationId == organizationId &&
                  attempt.Status == SubmissionAttemptStatus.Submitted &&
                  (!query.AssignmentId.HasValue || attempt.AssignmentId == query.AssignmentId.Value) &&
                  (!query.ClassId.HasValue || assignment.ClassId == query.ClassId.Value) &&
                  (!query.IsLate.HasValue || attempt.IsLate == query.IsLate.Value) &&
                  (!teacherMembershipId.HasValue || dbContext.TeacherAssignments.Any(teacher =>
                      teacher.OrganizationId == organizationId &&
                      teacher.ClassId == assignment.ClassId &&
                      teacher.TeacherMembershipId == teacherMembershipId.Value &&
                      teacher.Status == TeacherAssignmentStatus.Active &&
                      teacher.EndedAtUtc == null))
            orderby attempt.SubmittedAtUtc, attempt.Id
            select attempt;

        var attempts = await attemptsQuery.AsNoTracking().ToArrayAsync(cancellationToken);
        var results = new List<EvaluationQueueRecord>(attempts.Length);
        foreach (var attempt in attempts)
        {
            var aggregate = await LoadAggregateAsync(attempt, asNoTracking: true, cancellationToken);
            results.Add(new EvaluationQueueRecord(
                aggregate.Submission,
                aggregate.LatestEvaluation,
                aggregate.CurrentReleasedEvaluation));
        }

        return results;
    }

    private async Task<EvaluationAggregateRecord> LoadAggregateAsync(
        SubmissionAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var submission = await LoadSubmissionAsync(attempt, asNoTracking, cancellationToken);
        IQueryable<EvaluationRevision> evaluations = dbContext.EvaluationRevisions.Where(
            evaluation => evaluation.OrganizationId == attempt.OrganizationId &&
                          evaluation.SubmissionAttemptId == attempt.Id);
        IQueryable<GradeRelease> releases = dbContext.GradeReleases.Where(
            release => release.OrganizationId == attempt.OrganizationId &&
                       release.SubmissionAttemptId == attempt.Id);
        if (asNoTracking)
        {
            evaluations = evaluations.AsNoTracking();
            releases = releases.AsNoTracking();
        }

        var allEvaluations = await evaluations
            .OrderByDescending(evaluation => evaluation.RevisionNumber)
            .ToArrayAsync(cancellationToken);
        var latestEvaluation = allEvaluations.FirstOrDefault();
        var currentReleasedEvaluation = allEvaluations.SingleOrDefault(
            evaluation => evaluation.Status == EvaluationRevisionStatus.Released);
        GradeRelease? currentGradeRelease = null;
        if (currentReleasedEvaluation is not null)
        {
            currentGradeRelease = await releases.SingleOrDefaultAsync(
                release => release.EvaluationRevisionId == currentReleasedEvaluation.Id,
                cancellationToken);
        }

        return new EvaluationAggregateRecord(
            submission,
            latestEvaluation,
            currentReleasedEvaluation,
            currentGradeRelease);
    }

    private async Task<EvaluationSubmissionRecord> LoadSubmissionAsync(
        SubmissionAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Assignment> assignmentQuery = dbContext.Assignments.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.AssignmentId);
        IQueryable<AssignmentVersion> versionQuery = dbContext.AssignmentVersions.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.AssignmentVersionId);
        IQueryable<Enrollment> enrollmentQuery = dbContext.Enrollments.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.EnrollmentId);
        if (asNoTracking)
        {
            assignmentQuery = assignmentQuery.AsNoTracking();
            versionQuery = versionQuery.AsNoTracking();
            enrollmentQuery = enrollmentQuery.AsNoTracking();
        }

        var assignment = await assignmentQuery.SingleAsync(cancellationToken);
        var version = await versionQuery.SingleAsync(cancellationToken);
        var enrollment = await enrollmentQuery.SingleAsync(cancellationToken);
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
        var attachments = await (
            from attachment in dbContext.SubmissionAttachments
            join fileAsset in dbContext.FileAssets on attachment.FileAssetId equals fileAsset.Id
            where attachment.OrganizationId == attempt.OrganizationId &&
                  attachment.SubmissionAttemptId == attempt.Id
            orderby attachment.AttachedAtUtc, attachment.Id
            select new SubmissionAttachmentWithFileRecord(attachment, fileAsset))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return new EvaluationSubmissionRecord(
            attempt,
            assignment,
            version,
            enrollment,
            student.UserId,
            student.DisplayName,
            attachments);
    }

    private sealed class EfEvaluationTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
