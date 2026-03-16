using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Farol.Api.Common;

public static class AuthenticatedUser
{
    public static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var userIdValue = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out userId);
    }
}
