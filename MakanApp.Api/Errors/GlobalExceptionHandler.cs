using MakanApp.Application.Identity;
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
        var problemDetails = exception is IdentityException identityException
            ? CreateIdentityProblem(identityException)
            : CreateUnexpectedProblem(exception, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails CreateIdentityProblem(IdentityException exception)
    {
        var status = exception.Code switch
        {
            IdentityErrorCodes.AuthRequired => StatusCodes.Status401Unauthorized,
            IdentityErrorCodes.UsernameAlreadyExists => StatusCodes.Status409Conflict,
            IdentityErrorCodes.OtpAlreadyUsed => StatusCodes.Status409Conflict,
            IdentityErrorCodes.OtpExpired => StatusCodes.Status410Gone,
            IdentityErrorCodes.OtpTooManyAttempts => StatusCodes.Status429TooManyRequests,
            IdentityErrorCodes.OtpRateLimited => StatusCodes.Status429TooManyRequests,
            IdentityErrorCodes.SmsProviderUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        return new ProblemDetails
        {
            Status = status,
            Title = "Identity request failed",
            Detail = exception.Message,
            Type = $"urn:makan:problem:{exception.Code.ToLowerInvariant().Replace('_', '-')}",
            Extensions =
            {
                ["code"] = exception.Code
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