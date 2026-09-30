using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic")]
public sealed class SessionsController(IAcademicSessionService sessionService) : ControllerBase
{
    [HttpPost("classes/{classId:guid}/sessions")]
    [ProducesResponseType<SessionResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SessionResult>> Create(
        Guid classId,
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.CreateSessionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionResult>> Get(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.GetSessionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            cancellationToken));

    [HttpPatch("sessions/{sessionId:guid}")]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionResult>> Update(
        Guid sessionId,
        UpdateSessionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.UpdateSessionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            command,
            cancellationToken));

    [HttpPost("sessions/{sessionId:guid}/cancel")]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionResult>> Cancel(
        Guid sessionId,
        SessionVersionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.CancelSessionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            command,
            cancellationToken));

    [HttpPost("sessions/{sessionId:guid}/complete")]
    [ProducesResponseType<SessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionResult>> Complete(
        Guid sessionId,
        SessionVersionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.CompleteSessionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            command,
            cancellationToken));

    [HttpGet("sessions/{sessionId:guid}/attendance")]
    [ProducesResponseType<SessionAttendanceResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionAttendanceResult>> GetAttendance(
        Guid sessionId,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.GetSessionAttendanceAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            cancellationToken));

    [HttpPut("sessions/{sessionId:guid}/attendance")]
    [ProducesResponseType<IReadOnlyCollection<AttendanceEntryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AttendanceEntryResult>>> RecordAttendance(
        Guid sessionId,
        RecordAttendanceCommand command,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.RecordAttendanceAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            command,
            cancellationToken));

    [HttpPatch("sessions/{sessionId:guid}/attendance/{attendanceId:guid}")]
    [ProducesResponseType<AttendanceEntryResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceEntryResult>> CorrectAttendance(
        Guid sessionId,
        Guid attendanceId,
        CorrectAttendanceCommand command,
        CancellationToken cancellationToken) =>
        Ok(await sessionService.CorrectAttendanceAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            sessionId,
            attendanceId,
            command,
            cancellationToken));
}
