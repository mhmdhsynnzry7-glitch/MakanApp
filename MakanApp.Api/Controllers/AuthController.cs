using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IIdentityService identityService) : ControllerBase
{
    [HttpPost("otp/challenges")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType<RequestOtpResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<RequestOtpResult>> RequestOtp(
        RequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await identityService.RequestOtpAsync(command, cancellationToken);
        return Accepted(result);
    }

    [HttpPost("otp/verify")]
    [ProducesResponseType<VerifyOtpResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifyOtpResult>> VerifyOtp(
        VerifyOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await identityService.VerifyOtpAsync(command, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await identityService.LogoutAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return NoContent();
    }
}
