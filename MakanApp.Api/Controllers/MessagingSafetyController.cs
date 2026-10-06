using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/messaging")]
public sealed class MessagingSafetyController(IMessagingSafetyService safetyService) : ControllerBase
{
    [HttpPost("blocks/{userId:guid}")]
    [ProducesResponseType<UserBlockResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<UserBlockResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserBlockResult>> Block(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await safetyService.BlockUserAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            userId,
            cancellationToken);
        return result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("blocks/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unblock(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await safetyService.UnblockUserAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            userId,
            cancellationToken);
        return NoContent();
    }

    [HttpGet("blocks")]
    [ProducesResponseType<IReadOnlyCollection<BlockedUserResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<BlockedUserResult>>> GetBlocks(
        CancellationToken cancellationToken) =>
        Ok(await safetyService.GetBlockedUsersAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpGet("reports/{reportId:guid}")]
    [ProducesResponseType<AbuseReportReceiptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AbuseReportReceiptResult>> GetReport(
        Guid reportId,
        CancellationToken cancellationToken) =>
        Ok(await safetyService.GetMyReportAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            reportId,
            cancellationToken));
}
