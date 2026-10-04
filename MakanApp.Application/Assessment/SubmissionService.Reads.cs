using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class SubmissionService
{
    public async Task<IReadOnlyCollection<SubmissionAttemptResult>> GetMyAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var eligibility = await _store.GetEligibilityForStudentAsync(
            context.OrganizationId!.Value,
            assignmentId,
            context.UserId,
            cancellationToken);
        if (eligibility is null)
        {
            throw RecipientNotFound();
        }

        var attempts = await _store.GetAttemptsForStudentAsync(
            context.OrganizationId.Value,
            assignmentId,
            context.UserId,
            cancellationToken);
        return attempts.Select(attempt => ToResult(attempt, true)).ToArray();
    }

    public async Task<SubmissionAttemptResult> GetAttemptAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var record = await _store.GetAttemptAsync(
            context.OrganizationId!.Value,
            attemptId,
            cancellationToken) ?? throw SubmissionNotFound();

        var includeContent = context.ActiveRole switch
        {
            OrganizationRole.Student when record.StudentUserId == context.UserId => true,
            OrganizationRole.Parent when
                record.Attempt.Status == SubmissionAttemptStatus.Submitted &&
                context.SubjectOrganizationPersonId == record.Enrollment.LearnerOrganizationPersonId => false,
            OrganizationRole.Manager when record.Attempt.Status == SubmissionAttemptStatus.Submitted => true,
            OrganizationRole.Teacher when
                record.Attempt.Status == SubmissionAttemptStatus.Submitted &&
                await _store.HasActiveTeacherAssignmentAsync(
                    record.Attempt.OrganizationId,
                    record.Assignment.ClassId,
                    context.MembershipId!.Value,
                    cancellationToken) => true,
            _ => throw SubmissionNotFound()
        };

        return ToResult(record, includeContent);
    }

    public async Task<IReadOnlyCollection<SubmissionAttemptResult>> GetSubmittedAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var assignment = await _store.GetAssignmentAsync(
            context.OrganizationId!.Value,
            assignmentId,
            cancellationToken) ?? throw SubmissionNotFound();

        var allowed = context.ActiveRole switch
        {
            OrganizationRole.Manager => true,
            OrganizationRole.Teacher => await _store.HasActiveTeacherAssignmentAsync(
                assignment.OrganizationId,
                assignment.ClassId,
                context.MembershipId!.Value,
                cancellationToken),
            _ => false
        };
        if (!allowed)
        {
            throw SubmissionNotFound();
        }

        var attempts = await _store.GetSubmittedAttemptsAsync(
            context.OrganizationId.Value,
            assignmentId,
            cancellationToken);
        return attempts.Select(attempt => ToResult(attempt, true)).ToArray();
    }
}
