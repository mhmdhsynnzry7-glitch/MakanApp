using System.Security.Claims;
using MakanApp.Application.Identity;

namespace MakanApp.Api.Authentication;

public static class AuthenticatedSession
{
    public static Guid GetUserId(ClaimsPrincipal principal) =>
        ParseRequiredClaim(principal, ClaimTypes.NameIdentifier);

    public static Guid GetSessionId(ClaimsPrincipal principal) =>
        ParseRequiredClaim(principal, SessionAuthenticationDefaults.SessionIdClaim);

    private static Guid ParseRequiredClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        if (!Guid.TryParse(value, out var id))
        {
            throw new IdentityException(
                IdentityErrorCodes.AuthRequired,
                "برای ادامه باید وارد شوید.");
        }

        return id;
    }
}
