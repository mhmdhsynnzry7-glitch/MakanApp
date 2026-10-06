using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamService
{
    public async Task<ExamEditorDto> GetEditorAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var record = await _store.GetCurrentAsync(
            context.OrganizationId!.Value,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
        return ToEditor(record);
    }

    public async Task<ExamTeacherPreview> GetTeacherPreviewAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var record = await _store.GetCurrentAsync(
            context.OrganizationId!.Value,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
        return ToTeacherPreview(record);
    }

    public async Task<StudentSafeExamPreview> GetStudentPreviewAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        ExamAggregateRecord record;
        if (context.ActiveRole is OrganizationRole.Manager or OrganizationRole.Teacher)
        {
            record = await _store.GetCurrentAsync(
                context.OrganizationId!.Value,
                examId,
                cancellationToken) ?? throw ExamNotFound();
            await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
            return ToStudentSafePreview(record);
        }

        if (context.ActiveRole != OrganizationRole.Student)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamStudentPreviewNotAllowed,
                "محتوای پیش‌نمایش آزمون فقط برای دانش‌آموز مجاز یا مدیر/معلم کلاس قابل مشاهده است.");
        }

        record = await _store.GetLatestPublishedAsync(
            context.OrganizationId!.Value,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        if (!await _store.IsStudentActivelyEnrolledAsync(
                record.Exam.OrganizationId,
                record.Exam.ClassId,
                userId,
                cancellationToken))
        {
            throw ExamNotFound();
        }

        var nowUtc = UtcNow();
        if (nowUtc < record.Version.AvailableFromUtc || nowUtc > record.Version.AvailableUntilUtc)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamStudentPreviewNotAllowed,
                "محتوای آزمون خارج از بازه فعال قابل مشاهده نیست.");
        }

        return ToStudentSafePreview(record);
    }

    public async Task<IReadOnlyCollection<StudentExamSummary>> GetMyExamsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        var organizationId = context.OrganizationId!.Value;
        IReadOnlyCollection<ExamSummaryRecord> records = context.ActiveRole switch
        {
            OrganizationRole.Manager => await _store.GetForManagerAsync(
                organizationId,
                cancellationToken),
            OrganizationRole.Teacher => await _store.GetForTeacherAsync(
                organizationId,
                context.MembershipId!.Value,
                cancellationToken),
            OrganizationRole.Student => await _store.GetForStudentAsync(
                organizationId,
                userId,
                cancellationToken),
            OrganizationRole.Parent when context.SubjectOrganizationPersonId.HasValue =>
                await _store.GetForLearnerAsync(
                    organizationId,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => throw NotAllowed()
        };

        return records.Select(ToSummary).ToArray();
    }
}
