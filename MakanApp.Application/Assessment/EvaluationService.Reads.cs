using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class EvaluationService
{
    public async Task<IReadOnlyCollection<EvaluationQueueItemResult>> GetQueueAsync(
        Guid userId,
        Guid sessionId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        IReadOnlyCollection<EvaluationQueueRecord> records = context.ActiveRole switch
        {
            OrganizationRole.Manager => await _store.GetQueueForManagerAsync(
                context.OrganizationId!.Value,
                query,
                cancellationToken),
            OrganizationRole.Teacher => await _store.GetQueueForTeacherAsync(
                context.OrganizationId!.Value,
                context.MembershipId!.Value,
                query,
                cancellationToken),
            _ => throw EvaluationNotAllowed()
        };

        return records
            .Select(ToQueueResult)
            .Where(item => !query.ReviewStatus.HasValue || item.ReviewStatus == query.ReviewStatus.Value)
            .ToArray();
    }

    public async Task<SubmissionForEvaluationResult> GetSubmissionForEvaluationAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        await EnsureCanEvaluateAsync(context, aggregate.Submission, cancellationToken);
        EnsureSubmitted(aggregate.Submission.Attempt);
        return ToSubmissionResult(aggregate.Submission);
    }

    public async Task<EvaluatorEvaluationResult> GetEvaluationAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        await EnsureCanEvaluateAsync(context, aggregate.Submission, cancellationToken);
        EnsureSubmitted(aggregate.Submission.Attempt);
        var evaluation = aggregate.LatestEvaluation ?? throw EvaluationNotFound();
        var release = aggregate.CurrentGradeRelease?.EvaluationRevisionId == evaluation.Id
            ? aggregate.CurrentGradeRelease
            : null;
        return ToEvaluatorResult(evaluation, aggregate.Submission.Version.MaxScore, release);
    }

    public async Task<StudentReleasedResult> GetReleasedAssignmentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        if (context.ActiveRole != OrganizationRole.Student ||
            context.SubjectOrganizationPersonId.HasValue ||
            aggregate.Submission.StudentUserId != context.UserId)
        {
            throw EvaluationNotFound();
        }

        var evaluation = aggregate.CurrentReleasedEvaluation ?? throw GradeNotReleased();
        var release = aggregate.CurrentGradeRelease ?? throw GradeNotReleased();
        return new StudentReleasedResult(
            aggregate.Submission.Attempt.Id,
            aggregate.Submission.Assignment.Id,
            aggregate.Submission.Version.Id,
            evaluation.RevisionNumber,
            evaluation.Score,
            aggregate.Submission.Version.MaxScore,
            evaluation.LearnerFeedback,
            release.ReleasedAtUtc);
    }

    public async Task<ParentReleasedResult> GetGuardianReleasedAssignmentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        if (context.ActiveRole != OrganizationRole.Parent ||
            !context.SubjectOrganizationPersonId.HasValue ||
            context.SubjectOrganizationPersonId.Value != aggregate.Submission.Enrollment.LearnerOrganizationPersonId)
        {
            throw EvaluationNotFound();
        }

        var evaluation = aggregate.CurrentReleasedEvaluation ?? throw GradeNotReleased();
        var release = aggregate.CurrentGradeRelease ?? throw GradeNotReleased();
        return new ParentReleasedResult(
            aggregate.Submission.Attempt.Id,
            aggregate.Submission.Assignment.Id,
            aggregate.Submission.Version.Id,
            evaluation.RevisionNumber,
            evaluation.Score,
            aggregate.Submission.Version.MaxScore,
            evaluation.GuardianVisibleFeedback,
            release.ReleasedAtUtc);
    }
}
