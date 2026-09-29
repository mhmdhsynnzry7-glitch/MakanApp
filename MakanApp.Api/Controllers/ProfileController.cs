using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public sealed class ProfileController(IIdentityService identityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CurrentUserResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResult>> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var result = await identityService.GetCurrentUserAsync(
            AuthenticatedSession.GetUserId(User),
            cancellationToken);
        return Ok(result);
    }

    [HttpPatch("profile")]
    [ProducesResponseType<CurrentUserResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResult>> CompleteProfile(
        CompleteProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await identityService.CompleteProfileAsync(
            AuthenticatedSession.GetUserId(User),
            command,
            cancellationToken);
        return Ok(result);
    }
}
