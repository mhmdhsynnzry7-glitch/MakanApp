using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            AssessmentException assessmentException => CreateKnownProblem(
                assessmentException.Code,
                assessmentException.Message),
            AcademicException academicException => CreateKnownProblem(
                academicException.Code,
                academicException.Message),
            IdentityException identityException => CreateKnownProblem(
                identityException.Code,
                identityException.Message),
            MessagingException messagingException => CreateKnownProblem(
                messagingException.Code,
                messagingException.Message),
            OrganizationException organizationException => CreateKnownProblem(
                organizationException.Code,
                organizationException.Message),
            GuardianException guardianException => CreateKnownProblem(
                guardianException.Code,
                guardianException.Message),
            StorageException storageException => CreateKnownProblem(
                storageException.Code,
                storageException.Message),
            _ => CreateUnexpectedProblem(exception, httpContext.TraceIdentifier)
        };

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails CreateKnownProblem(string code, string message)
    {
        var status = code switch
        {
            IdentityErrorCodes.AuthRequired => StatusCodes.Status401Unauthorized,
            IdentityErrorCodes.UsernameAlreadyExists => StatusCodes.Status409Conflict,
            IdentityErrorCodes.OtpAlreadyUsed => StatusCodes.Status409Conflict,
            IdentityErrorCodes.OtpExpired => StatusCodes.Status410Gone,
            IdentityErrorCodes.OtpTooManyAttempts => StatusCodes.Status429TooManyRequests,
            IdentityErrorCodes.OtpRateLimited => StatusCodes.Status429TooManyRequests,
            IdentityErrorCodes.SmsProviderUnavailable => StatusCodes.Status503ServiceUnavailable,
            MessagingErrorCodes.ConversationNotFound => StatusCodes.Status404NotFound,
            MessagingErrorCodes.DirectRecipientNotAvailable => StatusCodes.Status404NotFound,
            MessagingErrorCodes.ConversationNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.MessageNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.MessagePublishNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.ConversationManagementNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.ParticipantNotActive => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.OrganizationScopeMismatch => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.DirectConversationConflict => StatusCodes.Status409Conflict,
            MessagingErrorCodes.ConversationCreationConflict => StatusCodes.Status409Conflict,
            MessagingErrorCodes.ConversationArchived => StatusCodes.Status409Conflict,
            MessagingErrorCodes.ParticipantAlreadyActive => StatusCodes.Status409Conflict,
            MessagingErrorCodes.LastOwnerRequired => StatusCodes.Status409Conflict,
            MessagingErrorCodes.OwnershipTransferConflict => StatusCodes.Status409Conflict,
            MessagingErrorCodes.MessageIdempotencyConflict => StatusCodes.Status409Conflict,
            MessagingErrorCodes.MessageNotFound => StatusCodes.Status404NotFound,
            MessagingErrorCodes.MessageNotEditable => StatusCodes.Status409Conflict,
            MessagingErrorCodes.MessageDeleted => StatusCodes.Status409Conflict,
            MessagingErrorCodes.MessageEditConflict => StatusCodes.Status412PreconditionFailed,
            MessagingErrorCodes.MessageAttachmentNotReady => StatusCodes.Status409Conflict,
            MessagingErrorCodes.MessageAttachmentNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.MessageReplyNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.MessageForwardNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.MessageForwardScopeNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.ReactionNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.MentionNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.PinNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.ChangeCursorInvalid => StatusCodes.Status400BadRequest,
            MessagingErrorCodes.ReadCursorInvalid => StatusCodes.Status400BadRequest,
            MessagingErrorCodes.DeliveryCursorInvalid => StatusCodes.Status400BadRequest,
            MessagingErrorCodes.RealtimeSubscriptionNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.SearchQueryRequired => StatusCodes.Status400BadRequest,
            MessagingErrorCodes.SearchQueryInvalid => StatusCodes.Status400BadRequest,
            MessagingErrorCodes.SearchCursorInvalid => StatusCodes.Status400BadRequest,
            MessagingErrorCodes.SearchNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.UserBlockNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.ReportNotAllowed => StatusCodes.Status403Forbidden,
            MessagingErrorCodes.ReportMessageNotFound => StatusCodes.Status404NotFound,
            MessagingErrorCodes.ReportIdempotencyConflict => StatusCodes.Status409Conflict,
            MessagingErrorCodes.ParticipantNotFound => StatusCodes.Status404NotFound,
            MessagingErrorCodes.OwnershipTransferNotFound => StatusCodes.Status404NotFound,
            OrganizationErrorCodes.OrganizationNotFound => StatusCodes.Status404NotFound,
            OrganizationErrorCodes.InvitationNotFound => StatusCodes.Status404NotFound,
            OrganizationErrorCodes.WorkspaceNotFound => StatusCodes.Status404NotFound,
            OrganizationErrorCodes.InvitationExpired => StatusCodes.Status410Gone,
            OrganizationErrorCodes.InvitationRevoked => StatusCodes.Status410Gone,
            OrganizationErrorCodes.InvitationAlreadyAccepted => StatusCodes.Status409Conflict,
            OrganizationErrorCodes.MembershipNotActive => StatusCodes.Status403Forbidden,
            OrganizationErrorCodes.RoleNotActive => StatusCodes.Status403Forbidden,
            OrganizationErrorCodes.WorkspaceNotAllowed => StatusCodes.Status403Forbidden,
            OrganizationErrorCodes.ConcurrencyConflict => StatusCodes.Status412PreconditionFailed,
            GuardianErrorCodes.GuardianRelationNotFound => StatusCodes.Status404NotFound,
            GuardianErrorCodes.ChildContextNotFound => StatusCodes.Status404NotFound,
            GuardianErrorCodes.ParentRoleRequired => StatusCodes.Status403Forbidden,
            GuardianErrorCodes.ChildContextNotAllowed => StatusCodes.Status403Forbidden,
            AcademicErrorCodes.AcademicPeriodNotFound => StatusCodes.Status404NotFound,
            AcademicErrorCodes.CourseNotFound => StatusCodes.Status404NotFound,
            AcademicErrorCodes.ClassNotFound => StatusCodes.Status404NotFound,
            AcademicErrorCodes.LearnerNotFound => StatusCodes.Status404NotFound,
            AcademicErrorCodes.ClassNotActive => StatusCodes.Status409Conflict,
            AcademicErrorCodes.ClassCapacityExceeded => StatusCodes.Status409Conflict,
            AcademicErrorCodes.EnrollmentAlreadyActive => StatusCodes.Status409Conflict,
            AcademicErrorCodes.EnrollmentNotActive => StatusCodes.Status409Conflict,
            AcademicErrorCodes.TeacherAssignmentAlreadyActive => StatusCodes.Status409Conflict,
            AcademicErrorCodes.TeacherAssignmentNotActive => StatusCodes.Status409Conflict,
            AcademicErrorCodes.TeacherNotAllowed => StatusCodes.Status403Forbidden,
            AcademicErrorCodes.ManagerRoleRequired => StatusCodes.Status403Forbidden,
            AcademicErrorCodes.AcademicReadNotAllowed => StatusCodes.Status403Forbidden,
            AcademicErrorCodes.ScheduleRuleNotFound => StatusCodes.Status404NotFound,
            AcademicErrorCodes.SessionNotFound => StatusCodes.Status404NotFound,
            AcademicErrorCodes.SessionNotActive => StatusCodes.Status409Conflict,
            AcademicErrorCodes.SessionCancelled => StatusCodes.Status409Conflict,
            AcademicErrorCodes.SessionTimeConflict => StatusCodes.Status409Conflict,
            AcademicErrorCodes.ClassSessionConflict => StatusCodes.Status409Conflict,
            AcademicErrorCodes.TeacherSessionConflict => StatusCodes.Status409Conflict,
            AcademicErrorCodes.AttendanceNotAllowed => StatusCodes.Status403Forbidden,
            AcademicErrorCodes.AttendanceAlreadyRecorded => StatusCodes.Status409Conflict,
            AcademicErrorCodes.AttendanceAlreadyChanged => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.AssignmentNotFound => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.AssignmentNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.AssignmentNotDraft => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.AssignmentAlreadyPublished => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.TeacherNotAssigned => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.ExamNotFound => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.ExamNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.ExamStudentPreviewNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.ExamNotDraft => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamAlreadyPublished => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamVersionLocked => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamQuestionOrderInvalid => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamNotPublished => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamNotAvailableYet => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamWindowClosed => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamAttemptsExhausted => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamAttemptNotFound => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.ExamAttemptNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.ExamQuestionSetInvalid => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.ExamStartIdempotencyConflict => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.SubmissionNotFound => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.AssignmentRecipientNotFound => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.SubmissionNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.SubmissionFileNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.SubmissionNotDraft => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.SubmissionDeadlinePassed => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.SubmissionAttemptsExhausted => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.SubmissionFileNotReady => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.SubmissionVersionMismatch => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.AssignmentNotSubmittable => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.EvaluationNotFound => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.GradeNotReleased => StatusCodes.Status404NotFound,
            AssessmentErrorCodes.EvaluationNotAllowed => StatusCodes.Status403Forbidden,
            AssessmentErrorCodes.EvaluationSubmissionNotFinal => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.EvaluationNotReadyForRelease => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.EvaluationAlreadyReleased => StatusCodes.Status409Conflict,
            AssessmentErrorCodes.GradeReleaseConflict => StatusCodes.Status409Conflict,
            StorageErrorCodes.FileNotFound => StatusCodes.Status404NotFound,
            StorageErrorCodes.FileNotReady => StatusCodes.Status409Conflict,
            StorageErrorCodes.FileNotAllowed => StatusCodes.Status403Forbidden,
            StorageErrorCodes.FileTooLarge => StatusCodes.Status413PayloadTooLarge,
            StorageErrorCodes.FileTypeNotAllowed => StatusCodes.Status415UnsupportedMediaType,
            StorageErrorCodes.FileStorageFailed => StatusCodes.Status503ServiceUnavailable,
            StorageErrorCodes.FileUploadFailed => StatusCodes.Status500InternalServerError,
            StorageErrorCodes.FileAlreadyDeleted => StatusCodes.Status409Conflict,
            StorageErrorCodes.FileInUse => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return new ProblemDetails
        {
            Status = status,
            Title = "Request failed",
            Detail = message,
            Type = $"urn:makan:problem:{code.ToLowerInvariant().Replace('_', '-')}",
            Extensions =
            {
                ["code"] = code
            }
        };
    }

    private ProblemDetails CreateUnexpectedProblem(Exception exception, string traceId)
    {
        logger.LogError(
            exception,
            "An unhandled error occurred while processing the request. TraceId: {TraceId}",
            traceId);

        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal server error",
            Detail = "An unexpected error occurred while processing the request.",
            Type = "urn:makan:problem:unexpected-error",
            Extensions =
            {
                ["code"] = "UNEXPECTED_ERROR"
            }
        };
    }
}
