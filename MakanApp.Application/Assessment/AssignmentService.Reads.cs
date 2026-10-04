using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class AssignmentService
{
    public async Task<AssignmentResult> GetAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var record = await _store.GetAssignmentAsync(
            context.OrganizationId!.Value,
            assignmentId,
            cancellationToken);
        if (record is null || !await CanReadAsync(context, record, cancellationToken))
        {
            throw AssignmentNotFound();
        }

        return ToResult(record.Assignment, record.Version, record.ClassTitle);
    }

    public async Task<IReadOnlyCollection<AssignmentResult>> GetForClassAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var organizationId = context.OrganizationId!.Value;
        if (await _store.GetClassAsync(organizationId, classId, cancellationToken) is null)
        {
            throw AssignmentNotFound();
        }

        IReadOnlyCollection<AssignmentWithVersionRecord> records = context.ActiveRole switch
        {
            OrganizationRole.Manager => await _store.GetForManagerAsync(
                organizationId,
                classId,
                cancellationToken),
            OrganizationRole.Teacher => await GetForTeacherAsync(
                context,
                classId,
                cancellationToken),
            OrganizationRole.Student => await _store.GetForStudentAsync(
                organizationId,
                classId,
                userId,
                cancellationToken),
            OrganizationRole.Parent when context.SubjectOrganizationPersonId.HasValue =>
                await _store.GetForLearnerAsync(
                    organizationId,
                    classId,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => throw NotAllowed()
        };

        return records
            .Select(record => ToResult(record.Assignment, record.Version, record.ClassTitle))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForTeacherAsync(
        AccessContext context,
        Guid classId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageClassAsync(context, classId, cancellationToken);
        return await _store.GetForTeacherAsync(
            context.OrganizationId!.Value,
            classId,
            context.MembershipId!.Value,
            cancellationToken);
    }

    private async Task<bool> CanReadAsync(
        AccessContext context,
        AssignmentWithVersionRecord record,
        CancellationToken cancellationToken) =>
        context.ActiveRole switch
        {
            OrganizationRole.Manager => true,
            OrganizationRole.Teacher => await _store.HasActiveTeacherAssignmentAsync(
                record.Assignment.OrganizationId,
                record.Assignment.ClassId,
                context.MembershipId!.Value,
                cancellationToken),
            OrganizationRole.Student when record.Assignment.IsRecipientVisible =>
                await _store.IsRecipientForStudentAsync(
                    record.Assignment.OrganizationId,
                    record.Version.Id,
                    context.UserId,
                    cancellationToken),
            OrganizationRole.Parent when
                record.Assignment.IsRecipientVisible &&
                context.SubjectOrganizationPersonId.HasValue =>
                await _store.IsRecipientForLearnerAsync(
                    record.Assignment.OrganizationId,
                    record.Version.Id,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => false
        };
}
