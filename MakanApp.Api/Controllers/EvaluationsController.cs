using MakanApp.Api.Authentication;
using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class EvaluationsController(IEvaluationService evaluationService) : ControllerBase
{
    [HttpGet("evaluations/queue")]
    [ProducesResponseType<IReadOnlyCollection<EvaluationQueueItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<EvaluationQueueItemResult>>> GetQueue(
        [FromQuery] Guid? assignmentId,
        [FromQuery] Guid? classId,
        [FromQuery] EvaluationReviewStatus? reviewStatus,
        [FromQuery] bool? isLate,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.GetQueueAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            new EvaluationQueueQuery(assignmentId, classId, reviewStatus, isLate),
            cancellationToken));

    [HttpGet("submission-attempts/{attemptId:guid}/evaluation/submission")]
    [ProducesResponseType<SubmissionForEvaluationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubmissionForEvaluationResult>> GetSubmission(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.GetSubmissionForEvaluationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpGet("submission-attempts/{attemptId:guid}/evaluation")]
    [ProducesResponseType<EvaluatorEvaluationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluatorEvaluationResult>> GetEvaluation(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.GetEvaluationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpPut("submission-attempts/{attemptId:guid}/evaluation")]
    [ProducesResponseType<EvaluatorEvaluationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluatorEvaluationResult>> SaveDraft(
        Guid attemptId,
        SaveEvaluationDraftCommand command,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.CreateOrUpdateDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpPost("submission-attempts/{attemptId:guid}/evaluation/release")]
    [ProducesResponseType<GradeReleaseResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GradeReleaseResult>> Release(
        Guid attemptId,
        ReleaseEvaluationCommand command,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.ReleaseAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpPost("submission-attempts/{attemptId:guid}/evaluation/corrections")]
    [ProducesResponseType<EvaluatorEvaluationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EvaluatorEvaluationResult>> Correct(
        Guid attemptId,
        CorrectReleasedEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await evaluationService.CorrectReleasedEvaluationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("submission-attempts/{attemptId:guid}/result")]
    [ProducesResponseType<StudentReleasedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentReleasedResult>> GetStudentResult(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.GetReleasedAssignmentResultAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpGet("submission-attempts/{attemptId:guid}/guardian-result")]
    [ProducesResponseType<ParentReleasedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ParentReleasedResult>> GetParentResult(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await evaluationService.GetGuardianReleasedAssignmentResultAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));
}
