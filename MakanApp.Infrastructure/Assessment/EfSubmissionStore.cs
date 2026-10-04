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

public sealed class EfSubmissionStore(MakanDbContext dbContext) : ISubmissionStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfSubmissionTransaction(transaction);
    }

    public async Task<SubmissionEligibilityRecord?> GetEligibilityForStudentForUpdateAsync(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken)
    {
        var candidate = await CreateEligibilityQuery(organizationId, assignmentId, studentUserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var assignment = await dbContext.Assignments
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[Assignments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {assignmentId}")
            .SingleAsync(cancellationToken);
        var version = await dbContext.AssignmentVersions
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AssignmentVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {candidate.Version.Id}")
            .SingleAsync(cancellationToken);
        var recipient = await dbContext.AssignmentRecipients
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[AssignmentRecipients] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {candidate.Recipient.Id}")
            .SingleAsync(cancellationToken);
        var enrollment = await dbContext.Enrollments
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {candidate.Enrollment.Id}")
            .SingleAsync(cancellationToken);
        var organizationPerson = await dbContext.OrganizationPersons
            .FromSqlInterpolated(
                $"SELECT * FROM [organization].[OrganizationPersons] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {enrollment.LearnerOrganizationPersonId}")
            .SingleAsync(cancellationToken);

        return new SubmissionEligibilityRecord(
            assignment,
            version,
            recipient,
            enrollment,
            studentUserId,
            organizationPerson.IsActive);
    }

    public Task<SubmissionEligibilityRecord?> GetEligibilityForStudentAsync(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken) =>
        CreateEligibilityQuery(organizationId, assignmentId, studentUserId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Assignment?> GetAssignmentAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        dbContext.Assignments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                assignment => assignment.OrganizationId == organizationId &&
                              assignment.Id == assignmentId,
                cancellationToken);

    public async Task<SubmissionAttemptRecord?> GetAttemptForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        return attempt is null
            ? null
            : await LoadAttemptRecordAsync(attempt, asNoTracking: false, cancellationToken);
    }

    public async Task<SubmissionAttemptRecord?> GetAttemptAsync(
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
            : await LoadAttemptRecordAsync(attempt, asNoTracking: true, cancellationToken);
    }

    public Task<SubmissionAttempt?> GetDraftForUpdateAsync(
        Guid organizationId,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken) =>
        dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [AssignmentRecipientId] = {assignmentRecipientId} AND [AssignmentVersionId] = {assignmentVersionId} AND [Status] = 1")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountSubmittedAttemptsForUpdateAsync(
        Guid organizationId,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken) =>
        dbContext.SubmissionAttempts
            .FromSqlInterpolated(
                $"SELECT * FROM [assessment].[SubmissionAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [AssignmentRecipientId] = {assignmentRecipientId} AND [AssignmentVersionId] = {assignmentVersionId} AND [Status] = 2")
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetAttemptsForStudentAsync(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken)
    {
        var attempts = await (
            from attempt in dbContext.SubmissionAttempts
            join enrollment in dbContext.Enrollments
                on new { attempt.OrganizationId, Id = attempt.EnrollmentId }
                equals new { enrollment.OrganizationId, enrollment.Id }
            join organizationPerson in dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where attempt.OrganizationId == organizationId &&
                  attempt.AssignmentId == assignmentId &&
                  user.Id == studentUserId
            orderby attempt.AttemptNumber
            select attempt)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        return await LoadAttemptRecordsAsync(attempts, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetSubmittedAttemptsAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var attempts = await dbContext.SubmissionAttempts
            .Where(attempt => attempt.OrganizationId == organizationId &&
                              attempt.AssignmentId == assignmentId &&
                              attempt.Status == SubmissionAttemptStatus.Submitted)
            .OrderBy(attempt => attempt.SubmittedAtUtc)
            .ThenBy(attempt => attempt.Id)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        return await LoadAttemptRecordsAsync(attempts, cancellationToken);
    }

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

    public void Add(SubmissionAttempt attempt) => dbContext.SubmissionAttempts.Add(attempt);
    public void Add(SubmissionAttachment attachment) => dbContext.SubmissionAttachments.Add(attachment);
    public void Remove(SubmissionAttachment attachment) => dbContext.SubmissionAttachments.Remove(attachment);

    public void SetOriginalRowVersion(SubmissionAttempt attempt, byte[] rowVersion) =>
        dbContext.Entry(attempt).Property(item => item.RowVersion).OriginalValue = rowVersion;

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
                "اطلاعات پیش‌نویس هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  (sqlException.Message.Contains("UX_SubmissionAttempts_OneDraft", StringComparison.Ordinal) ||
                   sqlException.Message.Contains("UX_SubmissionAttempts_Recipient_Version_Number", StringComparison.Ordinal)))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "یک تلاش هم‌زمان برای این تکلیف ثبت شده است؛ داده را دوباره دریافت کنید.");
        }
    }

    private IQueryable<SubmissionEligibilityRecord> CreateEligibilityQuery(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId) =>
        from assignment in dbContext.Assignments
        join version in dbContext.AssignmentVersions
            on new
            {
                assignment.OrganizationId,
                AssignmentId = assignment.Id,
                VersionNumber = assignment.CurrentVersionNumber
            }
            equals new
            {
                version.OrganizationId,
                version.AssignmentId,
                version.VersionNumber
            }
        join recipient in dbContext.AssignmentRecipients
            on new
            {
                version.OrganizationId,
                AssignmentVersionId = version.Id
            }
            equals new
            {
                recipient.OrganizationId,
                recipient.AssignmentVersionId
            }
        join enrollment in dbContext.Enrollments
            on new
            {
                recipient.OrganizationId,
                recipient.ClassId,
                Id = recipient.EnrollmentId
            }
            equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
        join organizationPerson in dbContext.OrganizationPersons
            on new
            {
                enrollment.OrganizationId,
                Id = enrollment.LearnerOrganizationPersonId
            }
            equals new { organizationPerson.OrganizationId, organizationPerson.Id }
        join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
        where assignment.OrganizationId == organizationId &&
              assignment.Id == assignmentId &&
              user.Id == studentUserId
        select new SubmissionEligibilityRecord(
            assignment,
            version,
            recipient,
            enrollment,
            user.Id,
            organizationPerson.Status == OrganizationPersonStatus.Active &&
            organizationPerson.EndedAtUtc == null);

    private async Task<IReadOnlyCollection<SubmissionAttemptRecord>> LoadAttemptRecordsAsync(
        IEnumerable<SubmissionAttempt> attempts,
        CancellationToken cancellationToken)
    {
        var results = new List<SubmissionAttemptRecord>();
        foreach (var attempt in attempts)
        {
            results.Add(await LoadAttemptRecordAsync(attempt, asNoTracking: true, cancellationToken));
        }

        return results;
    }

    private async Task<SubmissionAttemptRecord> LoadAttemptRecordAsync(
        SubmissionAttempt attempt,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var assignmentQuery = dbContext.Assignments.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.AssignmentId);
        var versionQuery = dbContext.AssignmentVersions.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.AssignmentVersionId);
        var recipientQuery = dbContext.AssignmentRecipients.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.AssignmentRecipientId);
        var enrollmentQuery = dbContext.Enrollments.Where(item =>
            item.OrganizationId == attempt.OrganizationId && item.Id == attempt.EnrollmentId);
        if (asNoTracking)
        {
            assignmentQuery = assignmentQuery.AsNoTracking();
            versionQuery = versionQuery.AsNoTracking();
            recipientQuery = recipientQuery.AsNoTracking();
            enrollmentQuery = enrollmentQuery.AsNoTracking();
        }

        var assignment = await assignmentQuery.SingleAsync(cancellationToken);
        var version = await versionQuery.SingleAsync(cancellationToken);
        var recipient = await recipientQuery.SingleAsync(cancellationToken);
        var enrollment = await enrollmentQuery.SingleAsync(cancellationToken);
        var personData = await (
            from organizationPerson in dbContext.OrganizationPersons
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.OrganizationId == attempt.OrganizationId &&
                  organizationPerson.Id == enrollment.LearnerOrganizationPersonId
            select new
            {
                StudentUserId = user.Id,
                IsActive = organizationPerson.Status == OrganizationPersonStatus.Active &&
                           organizationPerson.EndedAtUtc == null
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

        return new SubmissionAttemptRecord(
            attempt,
            assignment,
            version,
            recipient,
            enrollment,
            personData.StudentUserId,
            personData.IsActive,
            attachments);
    }

    private sealed class EfSubmissionTransaction(IDbContextTransaction transaction)
        : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
