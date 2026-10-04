using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Assessment;

public sealed record SaveSubmissionDraftCommand(
    string? AnswerText,
    string ExpectedRowVersion);

public sealed record AttachSubmissionFileCommand(
    Guid FileAssetId,
    string ExpectedRowVersion);

public sealed record FinalSubmitAssignmentCommand(string ExpectedRowVersion);

public sealed record SubmissionAttachmentResult(
    Guid FileAssetId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    FileAssetStatus FileStatus,
    DateTime AttachedAtUtc);

public sealed record SubmissionAttemptResult(
    Guid Id,
    Guid OrganizationId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid AssignmentRecipientId,
    Guid EnrollmentId,
    int AttemptNumber,
    SubmissionAttemptStatus Status,
    bool ContentVisible,
    string? AnswerText,
    IReadOnlyCollection<SubmissionAttachmentResult> Attachments,
    DateTime CreatedAtUtc,
    DateTime? LastSavedAtUtc,
    DateTime? SubmittedAtUtc,
    bool IsLate,
    string RowVersion);

public sealed record SubmissionReceipt(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    int AttemptNumber,
    DateTime SubmittedAtUtc,
    bool IsLate,
    SubmissionAttemptStatus Status,
    string RowVersion);

public sealed record SubmissionEligibilityRecord(
    Assignment Assignment,
    AssignmentVersion Version,
    AssignmentRecipient Recipient,
    MakanApp.Domain.Academic.Enrollment Enrollment,
    Guid StudentUserId,
    bool OrganizationPersonIsActive);

public sealed record SubmissionAttemptRecord(
    SubmissionAttempt Attempt,
    Assignment Assignment,
    AssignmentVersion Version,
    AssignmentRecipient Recipient,
    MakanApp.Domain.Academic.Enrollment Enrollment,
    Guid StudentUserId,
    bool OrganizationPersonIsActive,
    IReadOnlyCollection<SubmissionAttachmentWithFileRecord> Attachments);

public sealed record SubmissionAttachmentWithFileRecord(
    SubmissionAttachment Attachment,
    FileAsset FileAsset);
