using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class AssignmentService : IAssignmentService
{
    private readonly IAccessContextResolver _accessContextResolver;
    private readonly IAssignmentStore _store;
    private readonly TimeProvider _timeProvider;

    public AssignmentService(
        IAccessContextResolver accessContextResolver,
        IAssignmentStore store,
        TimeProvider timeProvider)
    {
        _accessContextResolver = accessContextResolver;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<AssignmentResult> CreateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CreateAssignmentDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await GetActiveClassForUpdateAsync(
            context.OrganizationId!.Value,
            classId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, classId, cancellationToken);

        var nowUtc = UtcNow();
        var assignment = Assignment.CreateDraft(
            context.OrganizationId.Value,
            classId,
            context.MembershipId!.Value,
            nowUtc);
        var version = CreateVersion(assignment, context.MembershipId.Value, command, nowUtc);

        _store.Add(assignment);
        _store.Add(version);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(assignment, version, academicClass.Title);
    }

    public async Task<AssignmentResult> UpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        UpdateAssignmentDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetAssignmentForUpdateAsync(
            context.OrganizationId!.Value,
            assignmentId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, record.Assignment.ClassId, cancellationToken);
        EnsureClassActive(record);
        EnsureDraft(record);
        _store.SetOriginalRowVersion(
            record.Assignment,
            DecodeRowVersion(command.ExpectedAssignmentRowVersion));
        _store.SetOriginalRowVersion(
            record.Version,
            DecodeRowVersion(command.ExpectedVersionRowVersion));

        var nowUtc = UtcNow();
        try
        {
            record.Version.UpdateDraft(
                command.Title,
                command.Description,
                command.DueAtUtc,
                command.AllowLateSubmission,
                command.MaxAttempts,
                nowUtc);
            record.Assignment.MarkDraftUpdated(nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(record.Assignment, record.Version, record.ClassTitle);
    }

    public async Task<PublishAssignmentResult> PublishAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        PublishAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetAssignmentForUpdateAsync(
            context.OrganizationId!.Value,
            assignmentId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, record.Assignment.ClassId, cancellationToken);
        EnsureClassActive(record);
        EnsureDraft(record);
        _store.SetOriginalRowVersion(
            record.Assignment,
            DecodeRowVersion(command.ExpectedAssignmentRowVersion));
        _store.SetOriginalRowVersion(
            record.Version,
            DecodeRowVersion(command.ExpectedVersionRowVersion));

        var nowUtc = UtcNow();
        var enrollmentIds = await _store.GetActiveEnrollmentIdsForUpdateAsync(
            record.Assignment.OrganizationId,
            record.Assignment.ClassId,
            cancellationToken);
        var recipients = enrollmentIds
            .Distinct()
            .Select(enrollmentId => AssignmentRecipient.Create(
                record.Assignment.OrganizationId,
                record.Assignment.ClassId,
                record.Assignment.Id,
                record.Version.Id,
                enrollmentId,
                nowUtc))
            .ToArray();

        try
        {
            record.Version.Publish(nowUtc);
            record.Assignment.Publish(nowUtc);
        }
        catch (InvalidOperationException exception)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentDueDateInvalid,
                exception.Message);
        }

        _store.AddRange(recipients);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PublishAssignmentResult(
            ToResult(record.Assignment, record.Version, record.ClassTitle),
            recipients.Length);
    }

    private AssignmentVersion CreateVersion(
        Assignment assignment,
        Guid membershipId,
        CreateAssignmentDraftCommand command,
        DateTime nowUtc)
    {
        try
        {
            return AssignmentVersion.CreateDraft(
                assignment.OrganizationId,
                assignment.ClassId,
                assignment.Id,
                assignment.CurrentVersionNumber,
                command.Title,
                command.Description,
                command.DueAtUtc,
                command.AllowLateSubmission,
                command.MaxAttempts,
                membershipId,
                nowUtc);
        }
        catch (Exception exception) when (IsDomainValidationException(exception))
        {
            throw MapValidationException(exception);
        }
    }

    private static void EnsureDraft(AssignmentWithVersionRecord record)
    {
        if (record.Assignment.Status == AssignmentStatus.Published || record.Version.IsPublished)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentAlreadyPublished,
                "تکلیف قبلاً منتشر شده و نسخه منتشرشده تغییرپذیر نیست.");
        }

        if (!record.Assignment.IsDraft)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.AssignmentNotDraft,
                "تکلیف در وضعیت پیش‌نویس نیست.");
        }
    }

    private static void EnsureClassActive(AssignmentWithVersionRecord record)
    {
        if (!record.ClassIsActive)
        {
            throw new AssessmentException(
                AcademicErrorCodes.ClassNotActive,
                "کلاس برای تغییر یا انتشار تکلیف فعال نیست.");
        }
    }
}
