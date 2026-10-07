using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamGradingService(
    IAccessContextResolver accessContextResolver,
    IExamGradingStore store,
    TimeProvider timeProvider) : IExamGradingService
{
    public async Task<ExamGradeDto> CreateOrGetDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        if (aggregate.LatestRevision is { IsMutable: true })
        {
            await transaction.CommitAsync(cancellationToken);
            return ToGradeDto(aggregate, aggregate.LatestRevision, aggregate.LatestQuestionGrades);
        }

        if (aggregate.LatestRevision is not null)
        {
            throw AlreadyReleased();
        }

        var nowUtc = UtcNow();
        var gradeRevision = ExamGradeRevision.CreateDraft(
            aggregate.Source.Attempt.OrganizationId,
            aggregate.Source.Attempt.Id,
            context.MembershipId!.Value,
            nowUtc);
        var questionGrades = aggregate.Source.Questions
            .Select(question => ExamQuestionGrade.Create(
                gradeRevision,
                aggregate.Source.Attempt,
                question.AttemptQuestion,
                question.Question,
                question.FinalAnswer,
                question.FinalRevision,
                context.MembershipId.Value,
                nowUtc))
            .ToArray();
        gradeRevision.Recalculate(questionGrades, aggregate.Source.Version.MaxScore, nowUtc);
        store.Add(gradeRevision);
        store.AddRange(questionGrades);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, gradeRevision, questionGrades);
    }

    public async Task<ExamGradeDto> GradeQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid attemptQuestionId,
        GradeExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var revision = RequireMutableRevision(aggregate);
        ApplyExpectedRowVersion(revision, command.ExpectedGradeRowVersion);
        var questionGrade = aggregate.LatestQuestionGrades.SingleOrDefault(
            item => item.ExamAttemptQuestionId == attemptQuestionId) ?? throw GradeNotFound();
        var nowUtc = UtcNow();
        try
        {
            questionGrade.ReviewManually(
                command.AwardedScore,
                command.LearnerFeedback,
                command.EvaluatorPrivateNote,
                context.MembershipId!.Value,
                nowUtc);
            revision.Recalculate(
                aggregate.LatestQuestionGrades,
                aggregate.Source.Version.MaxScore,
                nowUtc);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw MapValidationException(exception);
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, revision, aggregate.LatestQuestionGrades);
    }

    public async Task<ExamGradeDto> CompleteReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CompleteExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var revision = RequireMutableRevision(aggregate);
        ApplyExpectedRowVersion(revision, command.ExpectedGradeRowVersion);
        try
        {
            revision.CompleteReview(
                aggregate.LatestQuestionGrades,
                aggregate.Source.Version.MaxScore,
                command.LearnerFeedback,
                command.GuardianVisibleFeedback,
                command.EvaluatorPrivateNote,
                UtcNow());
        }
        catch (ExamGradeIncompleteException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeQuestionNotReviewed,
                "همه سؤال‌های نیازمند بازبینی دستی هنوز بررسی نشده‌اند.");
        }
        catch (ArgumentException exception)
        {
            throw MapValidationException(exception);
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, revision, aggregate.LatestQuestionGrades);
    }

    public async Task<ExamGradeReleaseDto> ReleaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        ReleaseExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClientOperationId == Guid.Empty)
        {
            throw ReleaseIdempotencyConflict();
        }

        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var requestHash = ComputeReleaseRequestHash(attemptId, command.ExpectedGradeRowVersion);
        var operationRelease = aggregate.Releases.SingleOrDefault(
            release => release.ClientOperationId == command.ClientOperationId);
        if (operationRelease is not null)
        {
            if (!string.Equals(operationRelease.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw ReleaseIdempotencyConflict();
            }

            var operationRevision = aggregate.Revisions.Single(
                revision => revision.Id == operationRelease.ExamGradeRevisionId);
            await transaction.CommitAsync(cancellationToken);
            return ToReleaseDto(operationRelease, operationRevision, aggregate.Source.Version.MaxScore);
        }

        var revision = aggregate.LatestRevision ?? throw GradeNotFound();
        if (revision.IsReleased)
        {
            var existingRelease = aggregate.Releases.SingleOrDefault(
                release => release.ExamGradeRevisionId == revision.Id) ?? throw ReleaseConflict();
            await transaction.CommitAsync(cancellationToken);
            return ToReleaseDto(existingRelease, revision, aggregate.Source.Version.MaxScore);
        }

        ApplyExpectedRowVersion(revision, command.ExpectedGradeRowVersion);
        var nowUtc = UtcNow();
        if (aggregate.CurrentReleasedRevision is not null)
        {
            if (revision.SupersedesExamGradeRevisionId != aggregate.CurrentReleasedRevision.Id)
            {
                throw ReleaseConflict();
            }

            aggregate.CurrentReleasedRevision.MarkSuperseded(nowUtc);
            await store.SaveChangesAsync(cancellationToken);
        }
        else if (revision.SupersedesExamGradeRevisionId.HasValue)
        {
            throw ReleaseConflict();
        }

        try
        {
            revision.Release(
                aggregate.LatestQuestionGrades,
                aggregate.Source.Version.MaxScore,
                nowUtc);
        }
        catch (ExamGradeIncompleteException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeIncomplete,
                "نمره آزمون برای انتشار کامل نیست.");
        }

        var release = ExamGradeRelease.Create(
            revision,
            context.MembershipId!.Value,
            command.ClientOperationId,
            requestHash,
            nowUtc);
        store.Add(release);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReleaseDto(release, revision, aggregate.Source.Version.MaxScore);
    }

    public async Task<ExamGradeDto> CorrectReleasedAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CorrectReleasedExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var aggregate = await GetAggregateForUpdateAsync(context, attemptId, cancellationToken);
        await EnsureCanGradeAsync(context, aggregate.Source, cancellationToken);
        EnsureFinalized(aggregate.Source.Attempt);
        var released = aggregate.CurrentReleasedRevision ?? throw GradeNotReleased();
        if (aggregate.LatestRevision?.Id != released.Id)
        {
            throw ReleaseConflict();
        }

        ApplyExpectedRowVersion(released, command.ExpectedReleasedGradeRowVersion);
        ExamGradeRevision correction;
        try
        {
            correction = ExamGradeRevision.CreateCorrection(
                released,
                context.MembershipId!.Value,
                command.CorrectionReason,
                UtcNow());
        }
        catch (ArgumentException exception)
        {
            throw MapValidationException(exception);
        }

        var copiedGrades = aggregate.LatestQuestionGrades
            .Select(grade => grade.CopyTo(correction))
            .ToArray();
        correction.Recalculate(copiedGrades, aggregate.Source.Version.MaxScore, UtcNow());
        store.Add(correction);
        store.AddRange(copiedGrades);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToGradeDto(aggregate, correction, copiedGrades);
    }
}
