using Microsoft.AspNetCore.Identity;
using WeeklySalesCoach.Auth;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class UserAuthServiceTests
{
    private static async Task<(TestDb Db, UserAuthService Auth)> SeededAsync()
    {
        var t = new TestDb();
        var hasher = new PasswordHasher<AppUser>();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, hasher);
        }
        return (t, new UserAuthService(t.Factory, hasher));
    }

    [Fact]
    public async Task Validates_credentials()
    {
        var (t, auth) = await SeededAsync();
        using var owned = t;

        Assert.NotNull(await auth.ValidateCredentialsAsync("ana", SeedData.DemoPassword));
        Assert.NotNull(await auth.ValidateCredentialsAsync("  ANA ", SeedData.DemoPassword));
        Assert.Null(await auth.ValidateCredentialsAsync("ana", "wrong-password"));
        Assert.Null(await auth.ValidateCredentialsAsync("nobody", SeedData.DemoPassword));
        Assert.Null(await auth.ValidateCredentialsAsync("", ""));
    }

    [Fact]
    public async Task Principal_carries_role_user_id_and_rep_id()
    {
        var (t, auth) = await SeededAsync();
        using var owned = t;
        var ana = (await auth.FindByUsernameAsync("ana"))!;
        var roberto = (await auth.FindByUsernameAsync("roberto"))!;

        var rep = UserAuthService.CreatePrincipal(ana);
        var manager = UserAuthService.CreatePrincipal(roberto);

        Assert.True(rep.IsInRole("Rep"));
        Assert.Equal("Ana Torres", rep.Identity!.Name);
        Assert.Equal(ana.Id, rep.GetUserId());
        Assert.Equal(ana.RepId, rep.GetRepId());
        Assert.True(manager.IsInRole("Manager"));
        Assert.Null(manager.GetRepId());
    }
}
