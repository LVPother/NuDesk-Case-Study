using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Auth;

public class UserAuthService(IDbContextFactory<AppDbContext> dbFactory, IPasswordHasher<AppUser> hasher)
{
    public const string RepIdClaim = "rep_id";

    public async Task<AppUser?> ValidateCredentialsAsync(string? username, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;
        var user = await FindByUsernameAsync(username, ct);
        if (user is null) return null;
        return hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed ? null : user;
    }

    public async Task<AppUser?> FindByUsernameAsync(string? username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        var normalized = username.Trim().ToLowerInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == normalized, ct);
    }

    public static ClaimsPrincipal CreatePrincipal(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.RepId is int repId)
            claims.Add(new Claim(RepIdClaim, repId.ToString(CultureInfo.InvariantCulture)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
