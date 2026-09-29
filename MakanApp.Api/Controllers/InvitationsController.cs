using MakanApp.Api.Authentication;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/invitations")]
public sealed class InvitationsController(IOrganizationService organizationService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<InvitationResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<InvitationResult>>> GetMyInvitations(
        CancellationToken cancellationToken)
    {
        var result = await organizationService.GetMyInvitationsAsync(
            AuthenticatedSession.GetUserId(User),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{invitationId:guid}/accept")]
    [ProducesResponseType<AcceptInvitationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AcceptInvitationResult>> AcceptInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.AcceptInvitationAsync(
            AuthenticatedSession.GetUserId(User),
            invitationId,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{invitationId:guid}/decline")]
    [ProducesResponseType<DeclineInvitationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeclineInvitationResult>> DeclineInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.DeclineInvitationAsync(
            AuthenticatedSession.GetUserId(User),
            invitationId,
            cancellationToken);
        return Ok(result);
    }
}
