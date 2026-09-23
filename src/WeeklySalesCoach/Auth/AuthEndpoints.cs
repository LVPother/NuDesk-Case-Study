using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/", (HttpContext http) =>
        {
            if (http.User.Identity?.IsAuthenticated != true) return Results.LocalRedirect("/login");
            return Results.LocalRedirect(http.User.IsInRole(nameof(UserRole.Manager)) ? "/dashboard" : "/my-feedback");
        });

        // The cookie handler URL-encodes a query string in AccessDeniedPath, so redirect from a plain path instead.
        app.MapGet("/access-denied", () => Results.LocalRedirect("/login?denied=1"));

        var auth = app.MapGroup("/auth");

        // Form posts from the static login page; antiforgery is validated by UseAntiforgery().
        auth.MapPost("/login", async ([FromForm] string? username, [FromForm] string? password, UserAuthService users, HttpContext http) =>
        {
            var user = await users.ValidateCredentialsAsync(username, password);
            if (user is null) return Results.LocalRedirect("/login?error=1");
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserAuthService.CreatePrincipal(user));
            return Results.LocalRedirect("/");
        });

        // Demo-only one-click sign-in. Disabled with Demo:EnableQuickLogin=false.
        auth.MapPost("/quick-login", async ([FromForm] string? username, UserAuthService users, IOptions<DemoOptions> demo, HttpContext http) =>
        {
            if (!demo.Value.EnableQuickLogin) return Results.NotFound();
            var user = await users.FindByUsernameAsync(username);
            if (user is null) return Results.LocalRedirect("/login?error=1");
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserAuthService.CreatePrincipal(user));
            return Results.LocalRedirect("/");
        });

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/login");
        });
    }
}
