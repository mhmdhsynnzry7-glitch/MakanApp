using MakanApp.Api.Authentication;
using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class ExamGradingsController(IExamGradingService gradingService) : ControllerBase
{
    [HttpGet("exams/{examId:guid}/grading")]
    [ProducesResponseType<IReadOnlyCollection<ExamGradingQueueItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ExamGradingQueueItemDto>>> GetQueue(
        Guid examId,
        [FromQuery] Guid? classId,
        [FromQuery] ExamGradingQueueStatus? status,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.GetQueueAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            new ExamGradingQueueQuery(classId, status),
            cancellationToken));

    [HttpGet("exam-attempts/{attemptId:guid}/grading")]
    [ProducesResponseType<ExamGradeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamGradeDto>> GetForGrading(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.GetForGradingAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpPost("exam-attempts/{attemptId:guid}/grading")]
    [ProducesResponseType<ExamGradeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamGradeDto>> Initialize(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.CreateOrGetDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpPatch("exam-attempts/{attemptId:guid}/grading/questions/{attemptQuestionId:guid}")]
    [ProducesResponseType<ExamGradeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamGradeDto>> GradeQuestion(
        Guid attemptId,
        Guid attemptQuestionId,
        GradeExamQuestionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.GradeQuestionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            attemptQuestionId,
            command,
            cancellationToken));

    [HttpPost("exam-attempts/{attemptId:guid}/grading/complete")]
    [ProducesResponseType<ExamGradeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamGradeDto>> Complete(
        Guid attemptId,
        CompleteExamGradeCommand command,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.CompleteReviewAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpPost("exam-attempts/{attemptId:guid}/grade/release")]
    [ProducesResponseType<ExamGradeReleaseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamGradeReleaseDto>> Release(
        Guid attemptId,
        ReleaseExamGradeCommand command,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.ReleaseAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken));

    [HttpPost("exam-attempts/{attemptId:guid}/grade/corrections")]
    [ProducesResponseType<ExamGradeDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamGradeDto>> Correct(
        Guid attemptId,
        CorrectReleasedExamGradeCommand command,
        CancellationToken cancellationToken)
    {
        var result = await gradingService.CorrectReleasedAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("exam-attempts/{attemptId:guid}/result")]
    [ProducesResponseType<StudentExamResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentExamResultDto>> GetStudentResult(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.GetStudentResultAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));

    [HttpGet("exam-attempts/{attemptId:guid}/guardian-result")]
    [ProducesResponseType<GuardianExamResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GuardianExamResultDto>> GetGuardianResult(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await gradingService.GetGuardianResultAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            attemptId,
            cancellationToken));
}
