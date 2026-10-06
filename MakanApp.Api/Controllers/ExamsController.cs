using MakanApp.Api.Authentication;
using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class ExamsController(IExamService examService) : ControllerBase
{
    [HttpPost("classes/{classId:guid}/exams")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamEditorDto>> CreateDraft(
        Guid classId,
        CreateExamDraftCommand command,
        CancellationToken cancellationToken)
    {
        var result = await examService.CreateDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("exams")]
    [ProducesResponseType<IReadOnlyCollection<StudentExamSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<StudentExamSummary>>> GetMyExams(
        CancellationToken cancellationToken) =>
        Ok(await examService.GetMyExamsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpGet("exams/{examId:guid}/editor")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamEditorDto>> GetEditor(
        Guid examId,
        CancellationToken cancellationToken) =>
        Ok(await examService.GetEditorAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            cancellationToken));

    [HttpGet("exams/{examId:guid}/teacher-preview")]
    [ProducesResponseType<ExamTeacherPreview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamTeacherPreview>> GetTeacherPreview(
        Guid examId,
        CancellationToken cancellationToken) =>
        Ok(await examService.GetTeacherPreviewAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            cancellationToken));

    [HttpPatch("exams/{examId:guid}")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamEditorDto>> UpdateDraft(
        Guid examId,
        UpdateExamDraftCommand command,
        CancellationToken cancellationToken) =>
        Ok(await examService.UpdateDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            command,
            cancellationToken));

    [HttpPost("exams/{examId:guid}/questions")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamEditorDto>> AddQuestion(
        Guid examId,
        AddExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await examService.AddQuestionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("exams/{examId:guid}/questions/{questionId:guid}")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamEditorDto>> UpdateQuestion(
        Guid examId,
        Guid questionId,
        UpdateExamQuestionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await examService.UpdateQuestionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            questionId,
            command,
            cancellationToken));

    [HttpDelete("exams/{examId:guid}/questions/{questionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteQuestion(
        Guid examId,
        Guid questionId,
        [FromBody] DeleteExamQuestionCommand command,
        CancellationToken cancellationToken)
    {
        await examService.DeleteQuestionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            questionId,
            command,
            cancellationToken);
        return NoContent();
    }

    [HttpGet("exams/{examId:guid}/student-preview")]
    [ProducesResponseType<StudentSafeExamPreview>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentSafeExamPreview>> GetStudentPreview(
        Guid examId,
        CancellationToken cancellationToken) =>
        Ok(await examService.GetStudentPreviewAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            cancellationToken));

    [HttpPost("exams/{examId:guid}/publish")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ExamEditorDto>> Publish(
        Guid examId,
        PublishExamCommand command,
        CancellationToken cancellationToken) =>
        Ok(await examService.PublishAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            command,
            cancellationToken));

    [HttpPost("exams/{examId:guid}/versions")]
    [ProducesResponseType<ExamEditorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ExamEditorDto>> CreateNextVersion(
        Guid examId,
        CreateNextExamVersionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await examService.CreateNextVersionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            examId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
