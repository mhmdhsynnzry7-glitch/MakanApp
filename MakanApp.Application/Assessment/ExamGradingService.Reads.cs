using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamGradingService
{
    public async Task<IReadOnlyCollection<ExamGradingQueueItemDto>> GetQueueAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        ExamGradingQueueQuery query,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        IReadOnlyCollection<ExamGradingQueueRecord> records = context.ActiveRole switch
        {
            OrganizationRole.Manager => await store.GetQueueForManagerAsync(
                context.OrganizationId!.Value,
                examId,
                query,
                cancellationToken),
            OrganizationRole.Teacher => await store.GetQueueForTeacherAsync(
                context.OrganizationId!.Value,
                context.MembershipId!.Value,
                examId,
                query,
                cancellationToken),
            _ => throw GradeNotAllowed()
        };

        return records
            .Select(ToQueueItem)
            .Where(item => !query.Status.HasValue || item.Status == query.Status.Value)
            .ToArray();
    }

    public async Task<ExamGradeDto> GetForGradingAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var revision = aggregate.LatestRevision ?? throw GradeNotFound();
        return ToGradeDto(aggregate, revision, aggregate.LatestQuestionGrades);
    }

    public async Task<StudentExamResultDto> GetStudentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        if (context.ActiveRole != OrganizationRole.Student ||
            context.SubjectOrganizationPersonId.HasValue ||
            aggregate.Source.StudentUserId != context.UserId)
        {
            throw GradeNotFound();
        }

        EnsureFinalized(aggregate.Source.Attempt);
        var releaseStatus = GetReleaseStatus(aggregate);
        var released = aggregate.CurrentReleasedRevision;
        var release = aggregate.CurrentRelease;
        return new StudentExamResultDto(
            aggregate.Source.Attempt.Id,
            aggregate.Source.Exam.Id,
            aggregate.Source.Version.Id,
            aggregate.Source.Attempt.AttemptNumber,
            aggregate.Source.Attempt.FinalizedAtUtc!.Value,
            releaseStatus,
            released?.RevisionNumber,
            release?.ReleasedAtUtc,
            released?.TotalScore,
            released is null ? null : aggregate.Source.Version.MaxScore,
            released?.LearnerFeedback);
    }

    public async Task<GuardianExamResultDto> GetGuardianResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var aggregate = await GetAggregateAsync(context, attemptId, cancellationToken);
        if (context.ActiveRole != OrganizationRole.Parent ||
            !context.SubjectOrganizationPersonId.HasValue ||
            context.SubjectOrganizationPersonId.Value !=
            aggregate.Source.Enrollment.LearnerOrganizationPersonId)
        {
            throw GradeNotFound();
        }

        EnsureFinalized(aggregate.Source.Attempt);
        var releaseStatus = GetReleaseStatus(aggregate);
        var released = aggregate.CurrentReleasedRevision;
        var release = aggregate.CurrentRelease;
        return new GuardianExamResultDto(
            aggregate.Source.Attempt.Id,
            aggregate.Source.Exam.Id,
            aggregate.Source.Version.Id,
            aggregate.Source.Attempt.AttemptNumber,
            aggregate.Source.Attempt.FinalizedAtUtc!.Value,
            releaseStatus,
            released?.RevisionNumber,
            release?.ReleasedAtUtc,
            released?.TotalScore,
            released is null ? null : aggregate.Source.Version.MaxScore,
            released?.GuardianVisibleFeedback);
    }

    private static ExamGradingQueueItemDto ToQueueItem(ExamGradingQueueRecord record)
    {
        var status = record.LatestRevision switch
        {
            null => ExamGradingQueueStatus.AwaitingGrading,
            { Status: ExamGradeRevisionStatus.ReadyForRelease } => ExamGradingQueueStatus.ReadyForRelease,
            { Status: ExamGradeRevisionStatus.Released } => ExamGradingQueueStatus.Released,
            { IsMutable: true, SupersedesExamGradeRevisionId: not null } =>
                ExamGradingQueueStatus.CorrectionDraft,
            _ => ExamGradingQueueStatus.Draft
        };
        return new ExamGradingQueueItemDto(
            record.Source.Attempt.Id,
            record.Source.Exam.Id,
            record.Source.Version.Id,
            record.Source.Exam.ClassId,
            record.Source.Version.Title,
            record.Source.StudentDisplayName,
            record.Source.Attempt.AttemptNumber,
            record.Source.Attempt.FinalizedAtUtc!.Value,
            record.Source.Version.MaxScore,
            status,
            record.LatestRevision?.RevisionNumber,
            record.Source.Questions.Any(question =>
                question.Question.Type == ExamQuestionType.Descriptive));
    }

    private static ExamResultReleaseStatus GetReleaseStatus(ExamGradeAggregateRecord aggregate) =>
        aggregate.CurrentReleasedRevision is not null && aggregate.CurrentRelease is not null
            ? ExamResultReleaseStatus.Released
            : aggregate.LatestRevision is null
                ? ExamResultReleaseStatus.AwaitingGrading
                : ExamResultReleaseStatus.AwaitingRelease;
}
