using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed class ExamAttemptService(
    IAccessContextResolver accessContextResolver,
    IExamAttemptStore store,
    IExamQuestionOrderRandomizer questionOrderRandomizer,
    TimeProvider timeProvider) : IExamAttemptService
{
    public async Task<StudentExamAttemptDto> StartAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        StartExamCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClientOperationId == Guid.Empty)
        {
            throw Error(
                AssessmentErrorCodes.ExamStartIdempotencyConflict,
                "شناسه عملیات شروع آزمون الزامی است.");
        }

        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var organizationId = access.OrganizationId!.Value;
        var nowUtc = UtcNow();
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var context = await store.GetStartContextForUpdateAsync(
            organizationId,
            examId,
            userId,
            cancellationToken) ?? throw ExamNotFound();

        if (context.Enrollment is null)
        {
            throw AttemptNotAllowed();
        }

        var operationAttempt = await store.GetByClientOperationForUpdateAsync(
            organizationId,
            context.Enrollment.Id,
            command.ClientOperationId,
            cancellationToken);
        if (operationAttempt is not null && operationAttempt.ExamId != examId)
        {
            throw Error(
                AssessmentErrorCodes.ExamStartIdempotencyConflict,
                "شناسه عملیات برای درخواست شروع دیگری استفاده شده است.");
        }

        var attempts = await store.GetAttemptsForUpdateAsync(
            organizationId,
            examId,
            context.Enrollment.Id,
            cancellationToken);
        var idempotentAttempt = operationAttempt;
        if (idempotentAttempt is not null)
        {
            ExpireIfNeeded(idempotentAttempt, nowUtc);
            await store.SaveChangesAsync(cancellationToken);
            var existingRecord = await store.GetByIdAsync(
                organizationId,
                idempotentAttempt.Id,
                cancellationToken) ?? throw AttemptNotFound();
            await transaction.CommitAsync(cancellationToken);
            return ToDto(existingRecord, nowUtc);
        }

        var activeAttempt = attempts.SingleOrDefault(attempt => attempt.IsInProgress);
        if (activeAttempt is not null && activeAttempt.EffectiveDeadlineUtc <= nowUtc)
        {
            activeAttempt.MarkExpired(nowUtc);
            activeAttempt = null;
        }

        if (activeAttempt is not null)
        {
            var activeRecord = await store.GetByIdAsync(
                organizationId,
                activeAttempt.Id,
                cancellationToken) ?? throw AttemptNotFound();
            await transaction.CommitAsync(cancellationToken);
            return ToDto(activeRecord, nowUtc);
        }

        var version = context.Version;
        if (version is null ||
            !version.IsPublished ||
            context.Exam.Status != ExamStatus.Published)
        {
            throw Error(AssessmentErrorCodes.ExamNotPublished, "آزمون منتشرشده‌ای برای شروع وجود ندارد.");
        }

        var attemptNumber = attempts.Count + 1;
        ExamAttempt attempt;
        try
        {
            attempt = ExamAttempt.Start(
                organizationId,
                examId,
                version.Id,
                context.Exam.ClassId,
                context.Enrollment.Id,
                command.ClientOperationId,
                attemptNumber,
                version.MaxAttempts,
                version.AvailableFromUtc,
                version.AvailableUntilUtc,
                version.DurationMinutes,
                nowUtc);
        }
        catch (ExamNotAvailableYetException)
        {
            throw Error(AssessmentErrorCodes.ExamNotAvailableYet, "بازه شروع آزمون هنوز آغاز نشده است.");
        }
        catch (ExamWindowClosedException)
        {
            throw Error(AssessmentErrorCodes.ExamWindowClosed, "بازه شروع آزمون پایان یافته است.");
        }
        catch (ExamAttemptsExhaustedException)
        {
            throw Error(AssessmentErrorCodes.ExamAttemptsExhausted, "تعداد تلاش‌های مجاز آزمون تمام شده است.");
        }

        if (context.Questions.Count == 0 || context.Questions.Any(
                question => question.OrganizationId != organizationId ||
                            question.ExamVersionId != version.Id))
        {
            throw Error(
                AssessmentErrorCodes.ExamQuestionSetInvalid,
                "مجموعه سؤال‌های نسخه آزمون معتبر نیست.");
        }

        var orderedQuestions = version.RandomizationPolicy switch
        {
            ExamRandomizationPolicy.None => context.Questions.OrderBy(question => question.Order).ToArray(),
            ExamRandomizationPolicy.QuestionOrder => questionOrderRandomizer.Randomize(context.Questions),
            _ => throw Error(
                AssessmentErrorCodes.ExamQuestionSetInvalid,
                "سیاست ترتیب سؤال‌های آزمون معتبر نیست.")
        };
        var attemptQuestions = orderedQuestions
            .Select((question, index) => ExamAttemptQuestion.Create(attempt, question.Id, index + 1))
            .ToArray();

        store.Add(attempt);
        store.AddRange(attemptQuestions);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(
            new ExamAttemptRecord(
                attempt,
                attemptQuestions.Select(attemptQuestion => new FrozenExamQuestionRecord(
                    attemptQuestion,
                    orderedQuestions.Single(question => question.Id == attemptQuestion.QuestionVersionId)))
                    .ToArray()),
            nowUtc);
    }

    public async Task<StudentExamAttemptDto> GetMyActiveAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var nowUtc = UtcNow();
        var record = await store.GetOwnedActiveAsync(
            access.OrganizationId!.Value,
            examId,
            userId,
            nowUtc,
            cancellationToken) ?? throw AttemptNotFound();
        return ToDto(record, nowUtc);
    }

    public async Task<StudentExamAttemptDto> GetAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var nowUtc = UtcNow();
        var record = await store.GetOwnedByIdAsync(
            access.OrganizationId!.Value,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        return ToDto(record, nowUtc);
    }

    public async Task<IReadOnlyCollection<StudentExamAttemptSummaryDto>> GetMyAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var nowUtc = UtcNow();
        var records = await store.GetOwnedAttemptsAsync(
            access.OrganizationId!.Value,
            examId,
            userId,
            cancellationToken);
        return records
            .OrderBy(record => record.Attempt.AttemptNumber)
            .Select(record => ToSummary(record.Attempt, nowUtc))
            .ToArray();
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

    private static void ExpireIfNeeded(ExamAttempt attempt, DateTime nowUtc)
    {
        if (attempt.IsInProgress && attempt.EffectiveDeadlineUtc <= nowUtc)
        {
            attempt.MarkExpired(nowUtc);
        }
    }

    private static StudentExamAttemptDto ToDto(ExamAttemptRecord record, DateTime serverNowUtc) =>
        new(
            record.Attempt.Id,
            record.Attempt.ExamId,
            record.Attempt.ExamVersionId,
            record.Attempt.AttemptNumber,
            record.Attempt.StartedAtUtc,
            record.Attempt.EffectiveDeadlineUtc,
            serverNowUtc,
            EffectiveStatus(record.Attempt, serverNowUtc),
            record.Questions
                .OrderBy(item => item.AttemptQuestion.DisplayOrder)
                .Select(item => new StudentExamAttemptQuestionDto(
                    item.AttemptQuestion.Id,
                    item.Question.Id,
                    item.AttemptQuestion.DisplayOrder,
                    item.Question.Type,
                    item.Question.Prompt,
                    item.Question.Score,
                    item.Question.Options
                        .OrderBy(option => option.Order)
                        .Select(option => new StudentExamAttemptOptionDto(
                            option.Id,
                            option.Order,
                            option.Text))
                        .ToArray()))
                .ToArray());

    private static StudentExamAttemptSummaryDto ToSummary(ExamAttempt attempt, DateTime serverNowUtc) =>
        new(
            attempt.Id,
            attempt.ExamVersionId,
            attempt.AttemptNumber,
            attempt.StartedAtUtc,
            attempt.EffectiveDeadlineUtc,
            serverNowUtc,
            EffectiveStatus(attempt, serverNowUtc));

    private static ExamAttemptStatus EffectiveStatus(ExamAttempt attempt, DateTime nowUtc) =>
        attempt.IsInProgress && attempt.EffectiveDeadlineUtc <= nowUtc
            ? ExamAttemptStatus.Expired
            : attempt.Status;

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static AssessmentException ExamNotFound() =>
        Error(AssessmentErrorCodes.ExamNotFound, "آزمون پیدا نشد.");

    private static AssessmentException AttemptNotFound() =>
        Error(AssessmentErrorCodes.ExamAttemptNotFound, "تلاش آزمون پیدا نشد.");

    private static AssessmentException AttemptNotAllowed() =>
        Error(AssessmentErrorCodes.ExamAttemptNotAllowed, "دسترسی به تلاش آزمون مجاز نیست.");

    private static AssessmentException Error(string code, string message) => new(code, message);
}
