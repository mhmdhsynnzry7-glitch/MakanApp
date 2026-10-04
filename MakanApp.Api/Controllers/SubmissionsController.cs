using MakanApp.Api.Authentication;
using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class SubmissionsController(ISubmissionService submissionService) : ControllerBase
{
    [HttpPost("assignments/{assignmentId:guid}/attempts")]
    [ProducesResponseType<SubmissionAttemptResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SubmissionAttemptResult>> CreateOrResumeDraft(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var result = await submissionService.CreateOrResumeDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("assignments/{assignmentId:guid}/attempts/me")]
    [ProducesResponseType<IReadOnlyCollection<SubmissionAttemptResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SubmissionAttemptResult>>> GetMyAttempts(
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.GetMyAttemptsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            cancellationToken));

    [HttpGet("submission-attempts/{attemptId:guid}")]
    [ProducesResponseType<SubmissionAttemptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionAttemptResult>> GetAttempt(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.GetAttemptAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpPatch("submission-attempts/{attemptId:guid}/draft")]
    [ProducesResponseType<SubmissionAttemptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionAttemptResult>> SaveDraft(
        Guid attemptId,
        SaveSubmissionDraftCommand command,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.SaveDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpPost("submission-attempts/{attemptId:guid}/attachments")]
    [ProducesResponseType<SubmissionAttemptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionAttemptResult>> AttachFile(
        Guid attemptId,
        AttachSubmissionFileCommand command,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.AttachFileAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpDelete("submission-attempts/{attemptId:guid}/attachments/{fileAssetId:guid}")]
    [ProducesResponseType<SubmissionAttemptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionAttemptResult>> RemoveFile(
        Guid attemptId,
        Guid fileAssetId,
        [FromQuery] string expectedRowVersion,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.RemoveFileAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            fileAssetId,
            expectedRowVersion,
            cancellationToken));

    [HttpPost("submission-attempts/{attemptId:guid}/submit")]
    [ProducesResponseType<SubmissionReceipt>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionReceipt>> FinalSubmit(
        Guid attemptId,
        FinalSubmitAssignmentCommand command,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.FinalSubmitAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpGet("assignments/{assignmentId:guid}/submitted-attempts")]
    [ProducesResponseType<IReadOnlyCollection<SubmissionAttemptResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SubmissionAttemptResult>>> GetSubmittedAttempts(
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        Ok(await submissionService.GetSubmittedAttemptsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            cancellationToken));
}
