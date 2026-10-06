using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamService : IExamService
{
    private readonly IAccessContextResolver _accessContextResolver;
    private readonly IExamStore _store;
    private readonly TimeProvider _timeProvider;

    public ExamService(
        IAccessContextResolver accessContextResolver,
        IExamStore store,
        TimeProvider timeProvider)
    {
        _accessContextResolver = accessContextResolver;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<ExamEditorDto> CreateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CreateExamDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await _store.GetClassForUpdateAsync(
            context.OrganizationId!.Value,
            classId,
            cancellationToken) ?? throw ExamNotFound();
        EnsureClassActive(academicClass.IsActive);
        await EnsureCanManageClassAsync(context, classId, cancellationToken);

        var nowUtc = UtcNow();
        var exam = Exam.CreateDraft(
            context.OrganizationId.Value,
            classId,
            context.MembershipId!.Value,
            nowUtc);
        ExamVersion version;
        try
        {
            version = ExamVersion.CreateDraft(
                exam.OrganizationId,
                exam.Id,
                exam.CurrentVersionNumber,
                command.Title,
                command.Description,
                command.AvailableFromUtc,
                command.AvailableUntilUtc,
                command.DurationMinutes,
                command.MaxAttempts,
                command.MaxScore,
                command.RandomizationPolicy,
                context.MembershipId.Value,
                nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        _store.Add(exam);
        _store.Add(version);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEditor(new ExamAggregateRecord(exam, version, academicClass.Title, true, []));
    }

    public async Task<ExamEditorDto> UpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        UpdateExamDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetCurrentForUpdateAsync(context, examId, cancellationToken);
        EnsureClassActive(record.ClassIsActive);
        EnsureDraft(record.Version);
        _store.SetOriginalRowVersion(record.Version, DecodeRowVersion(command.ExpectedVersionRowVersion));

        try
        {
            record.Version.UpdateDraft(
                command.Title,
                command.Description,
                command.AvailableFromUtc,
                command.AvailableUntilUtc,
                command.DurationMinutes,
                command.MaxAttempts,
                command.MaxScore,
                command.RandomizationPolicy,
                UtcNow());
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEditor(record);
    }

    public async Task<ExamEditorDto> AddQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        AddExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetCurrentForUpdateAsync(context, examId, cancellationToken);
        EnsureClassActive(record.ClassIsActive);
        EnsureDraft(record.Version);
        _store.SetOriginalRowVersion(record.Version, DecodeRowVersion(command.ExpectedVersionRowVersion));

        QuestionVersion question;
        try
        {
            var nowUtc = UtcNow();
            question = QuestionVersion.CreateDraft(
                record.Exam.OrganizationId,
                record.Version.Id,
                command.Order,
                command.Type,
                command.Prompt,
                command.Score,
                ToDefinitions(command.Options),
                nowUtc);
            record.Version.MarkQuestionsChanged(nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        _store.Add(question);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEditor(record with { Questions = record.Questions.Append(question).ToArray() });
    }

    public async Task<ExamEditorDto> UpdateQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        Guid questionId,
        UpdateExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetCurrentForUpdateAsync(context, examId, cancellationToken);
        EnsureClassActive(record.ClassIsActive);
        EnsureDraft(record.Version);
        var question = record.Questions.SingleOrDefault(item => item.Id == questionId)
            ?? throw ExamNotFound();
        _store.SetOriginalRowVersion(record.Version, DecodeRowVersion(command.ExpectedVersionRowVersion));
        _store.SetOriginalRowVersion(question, DecodeRowVersion(command.ExpectedQuestionRowVersion));
        var previousOptions = question.Options.ToArray();

        try
        {
            var nowUtc = UtcNow();
            question.UpdateDraft(
                record.Version,
                command.Order,
                command.Type,
                command.Prompt,
                command.Score,
                ToDefinitions(command.Options),
                nowUtc);
            record.Version.MarkQuestionsChanged(nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        _store.ReplaceOptions(question, previousOptions);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEditor(record);
    }

    public async Task DeleteQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        Guid questionId,
        DeleteExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetCurrentForUpdateAsync(context, examId, cancellationToken);
        EnsureClassActive(record.ClassIsActive);
        EnsureDraft(record.Version);
        var question = record.Questions.SingleOrDefault(item => item.Id == questionId)
            ?? throw ExamNotFound();
        _store.SetOriginalRowVersion(record.Version, DecodeRowVersion(command.ExpectedVersionRowVersion));
        _store.SetOriginalRowVersion(question, DecodeRowVersion(command.ExpectedQuestionRowVersion));
        record.Version.MarkQuestionsChanged(UtcNow());
        _store.Remove(question);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ExamEditorDto> PublishAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        PublishExamCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetCurrentForUpdateAsync(context, examId, cancellationToken);
        EnsureClassActive(record.ClassIsActive);
        if (record.Version.IsPublished)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamAlreadyPublished,
                "نسخه جاری آزمون قبلاً منتشر شده است.");
        }

        _store.SetOriginalRowVersion(record.Exam, DecodeRowVersion(command.ExpectedExamRowVersion));
        _store.SetOriginalRowVersion(record.Version, DecodeRowVersion(command.ExpectedVersionRowVersion));
        var nowUtc = UtcNow();
        if (record.Version.AvailableUntilUtc <= nowUtc)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamWindowInvalid,
                "پایان بازه آزمون هنگام انتشار باید در آینده باشد.");
        }

        try
        {
            record.Version.Publish(record.Questions, nowUtc);
            record.Exam.PublishCurrentVersion(record.Version.VersionNumber);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEditor(record);
    }

    public async Task<ExamEditorDto> CreateNextVersionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CreateNextExamVersionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetCurrentForUpdateAsync(context, examId, cancellationToken);
        EnsureClassActive(record.ClassIsActive);
        if (!record.Version.IsPublished)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamNotDraft,
                "تا پیش از انتشار نسخه جاری، نسخه جدید ساخته نمی‌شود.");
        }

        _store.SetOriginalRowVersion(record.Exam, DecodeRowVersion(command.ExpectedExamRowVersion));
        _store.SetOriginalRowVersion(
            record.Version,
            DecodeRowVersion(command.ExpectedPublishedVersionRowVersion));

        ExamVersion nextVersion;
        QuestionVersion[] copiedQuestions;
        try
        {
            var nowUtc = UtcNow();
            var nextVersionNumber = record.Exam.StartNextDraft(record.Version.VersionNumber);
            nextVersion = record.Version.CreateNextDraft(
                nextVersionNumber,
                context.MembershipId!.Value,
                nowUtc);
            copiedQuestions = record.Questions
                .OrderBy(question => question.Order)
                .Select(question => question.CopyTo(nextVersion.Id, nowUtc))
                .ToArray();
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        _store.Add(nextVersion);
        _store.AddRange(copiedQuestions);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToEditor(new ExamAggregateRecord(
            record.Exam,
            nextVersion,
            record.ClassTitle,
            record.ClassIsActive,
            copiedQuestions));
    }
}
