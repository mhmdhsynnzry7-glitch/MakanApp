using MakanApp.Api.Authentication;
using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class AssignmentsController(IAssignmentService assignmentService) : ControllerBase
{
    [HttpPost("classes/{classId:guid}/assignments")]
    [ProducesResponseType<AssignmentResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AssignmentResult>> CreateDraft(
        Guid classId,
        CreateAssignmentDraftCommand command,
        CancellationToken cancellationToken)
    {
        var result = await assignmentService.CreateDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("classes/{classId:guid}/assignments")]
    [ProducesResponseType<IReadOnlyCollection<AssignmentResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AssignmentResult>>> GetForClass(
        Guid classId,
        CancellationToken cancellationToken) =>
        Ok(await assignmentService.GetForClassAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            cancellationToken));

    [HttpGet("assignments")]
    [ProducesResponseType<IReadOnlyCollection<AssignmentResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AssignmentResult>>> GetAssignments(
        [FromQuery] Guid classId,
        CancellationToken cancellationToken) =>
        Ok(await assignmentService.GetForClassAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            cancellationToken));

    [HttpGet("assignments/{assignmentId:guid}")]
    [ProducesResponseType<AssignmentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssignmentResult>> Get(
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        Ok(await assignmentService.GetAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            cancellationToken));

    [HttpPatch("assignments/{assignmentId:guid}")]
    [ProducesResponseType<AssignmentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssignmentResult>> UpdateDraft(
        Guid assignmentId,
        UpdateAssignmentDraftCommand command,
        CancellationToken cancellationToken) =>
        Ok(await assignmentService.UpdateDraftAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            command,
            cancellationToken));

    [HttpPost("assignments/{assignmentId:guid}/publish")]
    [ProducesResponseType<PublishAssignmentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublishAssignmentResult>> Publish(
        Guid assignmentId,
        PublishAssignmentCommand command,
        CancellationToken cancellationToken) =>
        Ok(await assignmentService.PublishAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            command,
            cancellationToken));
}
