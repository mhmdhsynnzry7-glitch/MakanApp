using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conversations/{conversationId:guid}/messages/{messageId:guid}/reports")]
public sealed class MessageReportsController(IMessagingSafetyService safetyService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AbuseReportReceiptResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AbuseReportReceiptResult>> Report(
        Guid conversationId,
        Guid messageId,
        ReportMessageCommand command,
        CancellationToken cancellationToken)
    {
        var result = await safetyService.ReportMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
