using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed class ExamFinalizationService(
    IAccessContextResolver accessContextResolver,
    IExamFinalizationStore store,
    TimeProvider timeProvider) : IExamFinalizationService
{
    public async Task<ExamFinalReceiptDto> FinalizeAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        FinalizeExamCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClientOperationId == Guid.Empty)
        {
            throw Error(
                AssessmentErrorCodes.ExamFinalizeIdempotencyConflict,
                "شناسه عملیات نهایی‌سازی آزمون الزامی است.");
        }

        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var organizationId = access.OrganizationId!.Value;
        var nowUtc = UtcNow();
        var requestHash = ComputeRequestHash(attemptId, command);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var attempt = await store.GetOwnedAttemptForUpdateAsync(
            organizationId,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        if (!await store.IsSessionActiveForUpdateAsync(
                sessionId,
                userId,
                nowUtc,
                cancellationToken))
        {
            throw AttemptNotAllowed();
        }

        if (attempt.Status == ExamAttemptStatus.Finalized)
        {
            var finalized = await GetFinalizationRecordAsync(
                organizationId,
                attemptId,
                userId,
                cancellationToken);
            ValidateFinalizedRetry(attempt, command, requestHash);
            await transaction.CommitAsync(cancellationToken);
            return ToReceipt(finalized);
        }

        if (attempt.Status == ExamAttemptStatus.Expired)
        {
            throw DeadlinePassed();
        }

        if (attempt.IsInProgress && nowUtc >= attempt.EffectiveDeadlineUtc)
        {
            attempt.MarkExpired(nowUtc);
            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw DeadlinePassed();
        }

        try
        {
            attempt.EnsureWriteLease(sessionId, command.WriteLeaseVersion, nowUtc);
        }
        catch (Exception exception) when (IsLeaseDomainException(exception))
        {
            throw MapDomainException(exception);
        }

        if (attempt.AnswerSetVersion != command.ExpectedAnswerSetVersion)
        {
            throw Error(
                AssessmentErrorCodes.ExamAnswerSetVersionConflict,
                "نسخه مجموعه پاسخ‌ها تغییر کرده است؛ پاسخ‌های پذیرفته‌شده را دوباره دریافت کنید.");
        }

        var questions = await store.GetQuestionsForFinalizationAsync(
            organizationId,
            attemptId,
            cancellationToken);
        var finalAnswers = questions
            .Where(question => question.CurrentRevision is not null)
            .Select(question => ExamFinalAnswer.Create(
                attempt,
                question.AttemptQuestion,
                question.CurrentRevision!))
            .ToArray();

        try
        {
            attempt.Finalize(
                sessionId,
                command.WriteLeaseVersion,
                command.ExpectedAnswerSetVersion,
                command.ClientOperationId,
                requestHash,
                nowUtc);
        }
        catch (Exception exception) when (IsFinalizationDomainException(exception))
        {
            throw MapDomainException(exception);
        }

        store.AddRange(finalAnswers);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReceipt(
            attempt,
            questions.Count,
            questions.Count(question => question.CurrentRevision?.HasAnswer == true));
    }

    public async Task<ExamFinalReceiptDto> GetReceiptAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var record = await store.GetOwnedFinalizationAsync(
            access.OrganizationId!.Value,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        if (record.Attempt.Status != ExamAttemptStatus.Finalized)
        {
            throw Error(
                AssessmentErrorCodes.ExamAttemptNotFinalizable,
                "تلاش آزمون هنوز نهایی نشده است.");
        }

        return ToReceipt(record);
    }

    private async Task<AccessContext> GetStudentContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != OrganizationRole.Student)
        {
            throw AttemptNotAllowed();
        }

        return context;
    }

    private async Task<ExamFinalizationRecord> GetFinalizationRecordAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await store.GetOwnedFinalizationAsync(
            organizationId,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();

    private static void ValidateFinalizedRetry(
        ExamAttempt attempt,
        FinalizeExamCommand command,
        string requestHash)
    {
        if (attempt.FinalizeClientOperationId == command.ClientOperationId)
        {
            if (!string.Equals(attempt.FinalizeRequestHash, requestHash, StringComparison.Ordinal))
            {
                throw Error(
                    AssessmentErrorCodes.ExamFinalizeIdempotencyConflict,
                    "شناسه عملیات نهایی‌سازی قبلاً با payload دیگری استفاده شده است.");
            }

            return;
        }

        if (attempt.FinalizedAnswerSetVersion == command.ExpectedAnswerSetVersion &&
            attempt.WriteLeaseVersion == command.WriteLeaseVersion)
        {
            return;
        }

        throw Error(
            AssessmentErrorCodes.ExamAlreadyFinalized,
            "تلاش آزمون قبلاً با وضعیت دیگری نهایی شده است.");
    }

    private static ExamFinalReceiptDto ToReceipt(ExamFinalizationRecord record) =>
        ToReceipt(
            record.Attempt,
            record.Questions.Count,
            record.FinalAnswers.Count(answer => answer.Revision.HasAnswer));

    private static ExamFinalReceiptDto ToReceipt(
        ExamAttempt attempt,
        int totalQuestionCount,
        int answeredQuestionCount) =>
        new(
            attempt.Id,
            attempt.ExamId,
            attempt.ExamVersionId,
            attempt.AttemptNumber,
            attempt.StartedAtUtc,
            attempt.FinalizedAtUtc ?? throw new InvalidOperationException("زمان نهایی‌سازی ذخیره نشده است."),
            attempt.FinalizedAnswerSetVersion ?? throw new InvalidOperationException("نسخه نهایی پاسخ‌ها ذخیره نشده است."),
            answeredQuestionCount,
            totalQuestionCount,
            attempt.Status);

    private static string ComputeRequestHash(Guid attemptId, FinalizeExamCommand command)
    {
        var canonical = string.Join(
            '|',
            attemptId.ToString("N"),
            command.ExpectedAnswerSetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.WriteLeaseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool IsLeaseDomainException(Exception exception) =>
        exception is ExamAttemptNotWritableException or ExamAttemptDeadlinePassedException or
            ExamWriteLeaseRequiredException or ExamWriteLeaseHeldException or ExamWriteLeaseStaleException;

    private static bool IsFinalizationDomainException(Exception exception) =>
        IsLeaseDomainException(exception) ||
        exception is ExamAnswerSetVersionConflictException or ArgumentException;

    private static AssessmentException MapDomainException(Exception exception) => exception switch
    {
        ExamAttemptNotWritableException => Error(
            AssessmentErrorCodes.ExamAttemptNotFinalizable,
            "تلاش آزمون در وضعیت قابل نهایی‌سازی نیست."),
        ExamAttemptDeadlinePassedException => DeadlinePassed(),
        ExamWriteLeaseRequiredException => Error(
            AssessmentErrorCodes.ExamWriteLeaseRequired,
            "برای نهایی‌سازی آزمون باید write lease فعال دریافت شود."),
        ExamWriteLeaseHeldException => Error(
            AssessmentErrorCodes.ExamWriteLeaseHeldByOtherSession,
            "نشست دیگری write lease این تلاش را در اختیار دارد."),
        ExamWriteLeaseStaleException => Error(
            AssessmentErrorCodes.ExamWriteLeaseStale,
            "نسخه write lease منقضی شده است؛ وضعیت جاری را دوباره دریافت کنید."),
        ExamAnswerSetVersionConflictException => Error(
            AssessmentErrorCodes.ExamAnswerSetVersionConflict,
            "نسخه مجموعه پاسخ‌ها تغییر کرده است؛ پاسخ‌های پذیرفته‌شده را دوباره دریافت کنید."),
        _ => Error(
            AssessmentErrorCodes.ExamFinalizeConflict,
            "نهایی‌سازی آزمون با وضعیت جاری سازگار نیست.")
    };

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static AssessmentException AttemptNotFound() =>
        Error(AssessmentErrorCodes.ExamAttemptNotFound, "تلاش آزمون پیدا نشد.");

    private static AssessmentException AttemptNotAllowed() =>
        Error(AssessmentErrorCodes.ExamAttemptNotAllowed, "دسترسی به تلاش آزمون مجاز نیست.");

    private static AssessmentException DeadlinePassed() =>
        Error(AssessmentErrorCodes.ExamDeadlinePassed, "مهلت نهایی‌سازی آزمون پایان یافته است.");

    private static AssessmentException Error(string code, string message) => new(code, message);
}
