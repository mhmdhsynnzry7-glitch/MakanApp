using MakanApp.Application.Academic;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
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
            AcademicException academicException => CreateKnownProblem(
                academicException.Code,
                academicException.Message),
            IdentityException identityException => CreateKnownProblem(
                identityException.Code,
                identityException.Message),
            OrganizationException organizationException => CreateKnownProblem(
                organizationException.Code,
                organizationException.Message),
            GuardianException guardianException => CreateKnownProblem(
                guardianException.Code,
                guardianException.Message),
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
