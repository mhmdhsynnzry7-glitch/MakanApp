using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conversations")]
public sealed class ConversationsController(IMessagingService messagingService) : ControllerBase
{
    [HttpPost("direct")]
    [ProducesResponseType<DirectConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<DirectConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DirectConversationResult>> StartDirect(
        StartDirectConversationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await messagingService.StartOrGetDirectConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ConversationSummaryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ConversationSummaryResult>>> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await messagingService.GetMyConversationsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType<ConversationMessagePageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationMessagePageResult>> GetMessages(
        Guid conversationId,
        [FromQuery] long? beforeSequence,
        [FromQuery] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.GetConversationMessagesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            new GetConversationMessagesQuery(beforeSequence, limit),
            cancellationToken));

    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType<MessageReceiptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageReceiptResult>> SendTextMessage(
        Guid conversationId,
        SendTextMessageCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.SendTextMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));
}
