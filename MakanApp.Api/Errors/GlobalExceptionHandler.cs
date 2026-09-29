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
            IdentityException identityException => CreateKnownProblem(
                identityException.Code,
                identityException.Message),
            OrganizationException organizationException => CreateKnownProblem(
                organizationException.Code,
                organizationException.Message),
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