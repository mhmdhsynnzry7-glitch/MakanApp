using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Assessment;

public sealed partial class SubmissionService
{
    private async Task<AccessContext> GetOrganizationContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            !context.ActiveRole.HasValue)
        {
            throw SubmissionNotAllowed();
        }

        return context;
    }

    private async Task<AccessContext> GetStudentContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, sessionId, cancellationToken);
        if (context.ActiveRole != OrganizationRole.Student ||
            context.SubjectOrganizationPersonId.HasValue)
        {
            throw SubmissionNotAllowed();
        }

        return context;
    }

    private async Task<SubmissionAttemptRecord> GetOwnedAttemptForUpdateAsync(
        AccessContext context,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var record = await _store.GetAttemptForUpdateAsync(
            context.OrganizationId!.Value,
            attemptId,
            cancellationToken) ?? throw SubmissionNotFound();
        if (record.StudentUserId != context.UserId)
        {
            throw SubmissionNotFound();
        }

        return record;
    }

    private static void EnsureEligibleForMutation(SubmissionEligibilityRecord record)
    {
        if (!record.Enrollment.IsActive || !record.OrganizationPersonIsActive)
        {
            throw SubmissionNotAllowed();
        }

        EnsureSubmittable(
            record.Assignment,
            record.Version,
            record.Recipient,
            record.Enrollment.Id);
    }

    private static void EnsureEligibleForMutation(SubmissionAttemptRecord record)
    {
        if (!record.Enrollment.IsActive || !record.OrganizationPersonIsActive)
        {
            throw SubmissionNotAllowed();
        }

        EnsureSubmittable(
            record.Assignment,
            record.Version,
            record.Recipient,
            record.Attempt.EnrollmentId);
        if (record.Attempt.AssignmentId != record.Assignment.Id ||
            record.Attempt.AssignmentVersionId != record.Version.Id ||
            record.Attempt.AssignmentRecipientId != record.Recipient.Id)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionVersionMismatch,
                "نسخه یا مخاطب تلاش با تکلیف منتشرشده سازگار نیست.");
        }
    }

    private static void EnsureSubmittable(
        Assignment assignment,
        AssignmentVersion version,
        AssignmentRecipient recipient,
        Guid enrollmentId)
    {
        if (assignment.Status != AssignmentStatus.Published || !version.IsPublished)
        {
            throw Error(
                AssessmentErrorCodes.AssignmentNotSubmittable,
                "تکلیف برای ارسال پاسخ فعال نیست.");
        }

        if (version.AssignmentId != assignment.Id ||
            recipient.AssignmentId != assignment.Id ||
            recipient.AssignmentVersionId != version.Id ||
            recipient.EnrollmentId != enrollmentId ||
            version.OrganizationId != assignment.OrganizationId ||
            recipient.OrganizationId != assignment.OrganizationId)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionVersionMismatch,
                "نسخه یا مخاطب تلاش با تکلیف منتشرشده سازگار نیست.");
        }
    }

    private static void EnsureDeadlineAllowsDraft(AssignmentVersion version, DateTime nowUtc)
    {
        if (nowUtc > version.DueAtUtc && !version.AllowLateSubmission)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionDeadlinePassed,
                "مهلت ایجاد یا ذخیره پیش‌نویس این تکلیف گذشته است.");
        }
    }

    private static void EnsureDraft(SubmissionAttempt attempt)
    {
        if (!attempt.IsDraft)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionNotDraft,
                "تلاش ارسال‌شده تغییرپذیر نیست.");
        }
    }

    private static void EnsureFileCanBeAttached(FileAsset fileAsset, AccessContext context)
    {
        if (fileAsset.UploadedByUserId != context.UserId ||
            fileAsset.OrganizationId != context.OrganizationId)
        {
            throw FileNotAllowed();
        }

        if (fileAsset.Status != FileAssetStatus.Ready)
        {
            throw FileNotReady();
        }
    }

    private static void EnsureFileCanBeFinalized(FileAsset fileAsset, AccessContext context)
    {
        if (fileAsset.UploadedByUserId != context.UserId ||
            fileAsset.OrganizationId != context.OrganizationId)
        {
            throw FileNotAllowed();
        }

        if (fileAsset.Status != FileAssetStatus.Ready)
        {
            throw FileNotReady();
        }
    }

    private void ApplyExpectedRowVersion(SubmissionAttempt attempt, string expectedRowVersion)
    {
        var rowVersion = DecodeRowVersion(expectedRowVersion);
        if (!attempt.RowVersion.SequenceEqual(rowVersion))
        {
            throw Error(
                AssessmentErrorCodes.ConcurrencyConflict,
                "پیش‌نویس هم‌زمان تغییر کرده است؛ اطلاعات را دوباره دریافت کنید.");
        }

        _store.SetOriginalRowVersion(attempt, rowVersion);
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            var rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length == 8 ? rowVersion : throw new FormatException();
        }
        catch (FormatException)
        {
            throw Error(
                AssessmentErrorCodes.ConcurrencyConflict,
                "نسخه پیش‌نویس معتبر نیست.");
        }
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static SubmissionAttemptRecord ToRecord(
        SubmissionAttempt attempt,
        SubmissionEligibilityRecord eligibility) =>
        new(
            attempt,
            eligibility.Assignment,
            eligibility.Version,
            eligibility.Recipient,
            eligibility.Enrollment,
            eligibility.StudentUserId,
            eligibility.OrganizationPersonIsActive,
            []);

    private static SubmissionAttemptResult ToResult(
        SubmissionAttemptRecord record,
        bool includeContent) =>
        new(
            record.Attempt.Id,
            record.Attempt.OrganizationId,
            record.Attempt.AssignmentId,
            record.Attempt.AssignmentVersionId,
            record.Attempt.AssignmentRecipientId,
            record.Attempt.EnrollmentId,
            record.Attempt.AttemptNumber,
            record.Attempt.Status,
            includeContent,
            includeContent ? record.Attempt.AnswerText : null,
            includeContent
                ? record.Attachments.Select(ToAttachmentResult).ToArray()
                : [],
            record.Attempt.CreatedAtUtc,
            record.Attempt.LastSavedAtUtc,
            record.Attempt.SubmittedAtUtc,
            record.Attempt.IsLate,
            Convert.ToBase64String(record.Attempt.RowVersion));

    private static SubmissionAttachmentResult ToAttachmentResult(
        SubmissionAttachmentWithFileRecord record) =>
        new(
            record.FileAsset.Id,
            record.FileAsset.OriginalFileName,
            record.FileAsset.ContentType,
            record.FileAsset.SizeBytes,
            record.FileAsset.Status,
            record.Attachment.AttachedAtUtc);

    private static SubmissionReceipt ToReceipt(SubmissionAttempt attempt) =>
        new(
            attempt.Id,
            attempt.AssignmentId,
            attempt.AssignmentVersionId,
            attempt.AttemptNumber,
            attempt.SubmittedAtUtc ?? throw new InvalidOperationException("رسید فقط برای تلاش ارسال‌شده ساخته می‌شود."),
            attempt.IsLate,
            attempt.Status,
            Convert.ToBase64String(attempt.RowVersion));

    private static AssessmentException SubmissionNotFound() =>
        Error(AssessmentErrorCodes.SubmissionNotFound, "تلاش ارسال پیدا نشد.");

    private static AssessmentException SubmissionNotAllowed() =>
        Error(AssessmentErrorCodes.SubmissionNotAllowed, "دسترسی به این تلاش ارسال مجاز نیست.");

    private static AssessmentException RecipientNotFound() =>
        Error(AssessmentErrorCodes.AssignmentRecipientNotFound, "مخاطب مجاز تکلیف پیدا نشد.");

    private static AssessmentException FileNotReady() =>
        Error(AssessmentErrorCodes.SubmissionFileNotReady, "فایل برای ارسال نهایی آماده نیست.");

    private static AssessmentException FileNotAllowed() =>
        Error(AssessmentErrorCodes.SubmissionFileNotAllowed, "استفاده از این فایل در پاسخ مجاز نیست.");

    private static AssessmentException Error(string code, string message) => new(code, message);
}
