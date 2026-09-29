using MakanApp.Api.Authentication;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/guardian")]
public sealed class GuardianController(IGuardianService guardianService) : ControllerBase
{
    [HttpGet("children")]
    [ProducesResponseType<IReadOnlyCollection<AuthorizedChildResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AuthorizedChildResult>>> GetMyChildren(
        CancellationToken cancellationToken)
    {
        var result = await guardianService.GetMyAuthorizedChildrenAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("children/{organizationPersonId:guid}/select")]
    [ProducesResponseType<AccessContext>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccessContext>> SelectChildContext(
        Guid organizationPersonId,
        CancellationToken cancellationToken)
    {
        var result = await guardianService.SelectChildContextAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationPersonId,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("relations/{organizationPersonId:guid}")]
    [ProducesResponseType<GuardianRelationSummaryResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<GuardianRelationSummaryResult>> GetRelationSummary(
        Guid organizationPersonId,
        CancellationToken cancellationToken)
    {
        var result = await guardianService.GetGuardianRelationSummaryAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationPersonId,
            cancellationToken);
        return Ok(result);
    }
}
