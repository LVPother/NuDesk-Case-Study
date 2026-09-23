using System.Globalization;
using System.Security.Claims;

namespace WeeklySalesCoach.Auth;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) => ParseInt(user.FindFirstValue(ClaimTypes.NameIdentifier));

    /// <summary>The signed-in rep's id. Comes from the auth cookie, never from the URL.</summary>
    public static int? GetRepId(this ClaimsPrincipal user) => ParseInt(user.FindFirstValue(UserAuthService.RepIdClaim));

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
}
