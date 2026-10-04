using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed partial class EvaluationService : IEvaluationService
{
    private readonly IAccessContextResolver _accessContextResolver;
    private readonly IEvaluationStore _store;
    private readonly TimeProvider _timeProvider;

    public EvaluationService(
        IAccessContextResolver accessContextResolver,
        IEvaluationStore store,
        TimeProvider timeProvider)
    {
        _accessContextResolver = accessContextResolver;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<EvaluatorEvaluationResult> CreateOrUpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        SaveEvaluationDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanEvaluateAsync(context, aggregate.Submission, cancellationToken);
        EnsureSubmitted(aggregate.Submission.Attempt);

        var nowUtc = UtcNow();
        EvaluationRevision evaluation;
        if (aggregate.LatestEvaluation is null)
        {
            if (!string.IsNullOrWhiteSpace(command.ExpectedRowVersion))
            {
                throw ConcurrencyConflict();
            }

            try
            {
                evaluation = EvaluationRevision.CreateDraft(
                    aggregate.Submission.Attempt.OrganizationId,
                    aggregate.Submission.Attempt.Id,
                    1,
                    command.Score,
                    aggregate.Submission.Version.MaxScore,
                    command.LearnerFeedback,
                    command.GuardianVisibleFeedback,
                    command.TeacherPrivateNote,
                    context.MembershipId!.Value,
                    nowUtc);
            }
            catch (ArgumentException exception)
            {
                throw MapValidationException(exception);
            }

            _store.Add(evaluation);
        }
        else
        {
            evaluation = aggregate.LatestEvaluation;
            if (!evaluation.IsDraft)
            {
                throw EvaluationAlreadyReleased();
            }

            ApplyExpectedRowVersion(evaluation, command.ExpectedRowVersion);
            try
            {
                evaluation.UpdateDraft(
                    command.Score,
                    aggregate.Submission.Version.MaxScore,
                    command.LearnerFeedback,
                    command.GuardianVisibleFeedback,
                    command.TeacherPrivateNote,
                    nowUtc);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                throw MapValidationException(exception);
            }
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEvaluatorResult(evaluation, aggregate.Submission.Version.MaxScore, null);
    }

    public async Task<GradeReleaseResult> ReleaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        ReleaseEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanEvaluateAsync(context, aggregate.Submission, cancellationToken);
        EnsureSubmitted(aggregate.Submission.Attempt);

        var evaluation = aggregate.LatestEvaluation ?? throw new AssessmentException(
            AssessmentErrorCodes.EvaluationNotReadyForRelease,
            "ارزیابی آماده انتشار وجود ندارد.");
        if (evaluation.IsReleased)
        {
            var existingRelease = aggregate.CurrentGradeRelease;
            if (existingRelease is null || existingRelease.EvaluationRevisionId != evaluation.Id)
            {
                throw GradeReleaseConflict();
            }

            await transaction.CommitAsync(cancellationToken);
            return ToReleaseResult(existingRelease, evaluation);
        }

        if (!evaluation.IsDraft)
        {
            throw EvaluationAlreadyReleased();
        }

        ApplyExpectedRowVersion(evaluation, command.ExpectedRowVersion);
        try
        {
            EvaluationRevision.ValidateScore(evaluation.Score, aggregate.Submission.Version.MaxScore);
        }
        catch (ArgumentException exception)
        {
            throw MapValidationException(exception);
        }

        var nowUtc = UtcNow();
        if (aggregate.CurrentReleasedEvaluation is not null)
        {
            if (evaluation.SupersedesEvaluationRevisionId != aggregate.CurrentReleasedEvaluation.Id)
            {
                throw GradeReleaseConflict();
            }

            aggregate.CurrentReleasedEvaluation.MarkSuperseded(nowUtc);
            await _store.SaveChangesAsync(cancellationToken);
        }
        else if (evaluation.SupersedesEvaluationRevisionId.HasValue)
        {
            throw GradeReleaseConflict();
        }

        evaluation.Release(nowUtc);
        var gradeRelease = GradeRelease.Create(
            evaluation.OrganizationId,
            evaluation.SubmissionAttemptId,
            evaluation.Id,
            context.MembershipId!.Value,
            nowUtc);
        _store.Add(gradeRelease);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReleaseResult(gradeRelease, evaluation);
    }

    public async Task<EvaluatorEvaluationResult> CorrectReleasedEvaluationAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CorrectReleasedEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanEvaluateAsync(context, aggregate.Submission, cancellationToken);
        EnsureSubmitted(aggregate.Submission.Attempt);

        var released = aggregate.CurrentReleasedEvaluation ?? throw new AssessmentException(
            AssessmentErrorCodes.GradeNotReleased,
            "نتیجه منتشرشده‌ای برای اصلاح وجود ندارد.");
        if (aggregate.LatestEvaluation is null || aggregate.LatestEvaluation.Id != released.Id)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.GradeReleaseConflict,
                "یک اصلاح منتشرنشده از قبل وجود دارد.");
        }

        ApplyExpectedRowVersion(released, command.ExpectedReleasedEvaluationRowVersion);
        EvaluationRevision correction;
        try
        {
            correction = EvaluationRevision.CreateCorrection(
                aggregate.Submission.Attempt.OrganizationId,
                aggregate.Submission.Attempt.Id,
                released.RevisionNumber + 1,
                command.Score,
                aggregate.Submission.Version.MaxScore,
                command.LearnerFeedback,
                command.GuardianVisibleFeedback,
                command.TeacherPrivateNote,
                context.MembershipId!.Value,
                UtcNow(),
                released.Id,
                command.CorrectionReason);
        }
        catch (ArgumentException exception)
        {
            throw MapValidationException(exception);
        }

        _store.Add(correction);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEvaluatorResult(correction, aggregate.Submission.Version.MaxScore, null);
    }
}
