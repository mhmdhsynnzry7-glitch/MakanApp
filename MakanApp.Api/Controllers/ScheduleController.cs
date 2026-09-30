using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class ScheduleController(IAcademicSessionService sessionService) : ControllerBase
{
    [HttpGet("schedule")]
    [ProducesResponseType<IReadOnlyCollection<SessionResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SessionResult>>> GetSchedule(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.GetScheduleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            fromUtc,
            toUtc,
            cancellationToken));

    [HttpPost("classes/{classId:guid}/schedule-rules")]
    [ProducesResponseType<ScheduleRuleResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ScheduleRuleResult>> CreateRule(
        Guid classId,
        CreateScheduleRuleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.CreateScheduleRuleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("schedule-rules/{scheduleRuleId:guid}")]
    [ProducesResponseType<ScheduleRuleResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ScheduleRuleResult>> UpdateRule(
        Guid scheduleRuleId,
        UpdateScheduleRuleCommand command,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.UpdateScheduleRuleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            scheduleRuleId,
            command,
            cancellationToken));
}
