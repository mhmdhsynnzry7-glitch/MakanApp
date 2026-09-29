using MakanApp.Api.Authentication;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/workspaces")]
public sealed class WorkspacesController(IOrganizationService organizationService)
    : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyCollection<WorkspaceResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkspaceResult>>> GetMyWorkspaces(
        CancellationToken cancellationToken)
    {
        var result = await organizationService.GetMyWorkspacesAsync(
            AuthenticatedSession.GetUserId(User),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("select")]
    [ProducesResponseType<AccessContext>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessContext>> SelectWorkspace(
        SelectWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.SelectWorkspaceAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("current")]
    [ProducesResponseType<AccessContext>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessContext>> GetCurrentAccessContext(
        CancellationToken cancellationToken)
    {
        var result = await organizationService.GetCurrentAccessContextAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return Ok(result);
    }
}
