using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conversations")]
public sealed class ConversationsController(
    IMessagingService messagingService,
    IConversationManagementService managementService) : ControllerBase
{
    [HttpPost("groups")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ManagedConversationResult>> CreateGroup(
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken) =>
        CreatedOrExisting(await managementService.CreateGroupAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken));

    [HttpPost("channels")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ManagedConversationResult>> CreateChannel(
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken) =>
        CreatedOrExisting(await managementService.CreateChannelAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken));

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

    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> GetDetails(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.GetDetailsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/members")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> AddMember(
        Guid conversationId,
        AddConversationMemberCommand command,
        CancellationToken cancellationToken) =>
        Ok(await managementService.AddMemberAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/members/{targetUserId:guid}")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> RemoveMember(
        Guid conversationId,
        Guid targetUserId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.RemoveMemberAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            targetUserId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/leave")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> Leave(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.LeaveAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPatch("{conversationId:guid}/members/{targetUserId:guid}/role")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> ChangeMemberRole(
        Guid conversationId,
        Guid targetUserId,
        ChangeConversationMemberRoleCommand command,
        CancellationToken cancellationToken) =>
        Ok(await managementService.ChangeMemberRoleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            targetUserId,
            command,
            cancellationToken));

    [HttpPost("{conversationId:guid}/ownership-transfers")]
    [ProducesResponseType<OwnershipTransferResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OwnershipTransferResult>> StartOwnershipTransfer(
        Guid conversationId,
        StartOwnershipTransferCommand command,
        CancellationToken cancellationToken)
    {
        var result = await managementService.StartOwnershipTransferAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{conversationId:guid}/ownership-transfers/{transferId:guid}/accept")]
    [ProducesResponseType<OwnershipTransferResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnershipTransferResult>> AcceptOwnershipTransfer(
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.AcceptOwnershipTransferAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            transferId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/ownership-transfers/{transferId:guid}/decline")]
    [ProducesResponseType<OwnershipTransferResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnershipTransferResult>> DeclineOwnershipTransfer(
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.DeclineOwnershipTransferAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            transferId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/archive")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> Archive(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.ArchiveAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    private ActionResult<ManagedConversationResult> CreatedOrExisting(
        ManagedConversationResult result) =>
        result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
}
