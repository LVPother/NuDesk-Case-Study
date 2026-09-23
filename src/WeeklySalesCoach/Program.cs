using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Auth;
using WeeklySalesCoach.Components;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Services.Export;
using WeeklySalesCoach.Services.Notifications;

var enUs = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = enUs;
CultureInfo.DefaultThreadCurrentUICulture = enUs;

const string ExportDemoFlag = "--export-demo-feedback";
var exportDemo = args.Contains(ExportDemoFlag);
var builder = WebApplication.CreateBuilder(args.Where(a => a != ExportDemoFlag).ToArray());

builder.Services.Configure<DomainOptions>(builder.Configuration.GetSection(DomainOptions.Section));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));
builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection(ClaudeOptions.Section));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.Section));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.Section));

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// AI: Claude (default) or Gemini when a key is configured, otherwise the pre-generated demo feedback.
builder.Services.AddScoped<ClaudeFeedbackGenerator>();
builder.Services.AddHttpClient<GeminiFeedbackGenerator>((sp, client) =>
    client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<GeminiOptions>>().Value.TimeoutSeconds));
builder.Services.AddSingleton(sp => new DemoFeedbackGenerator(
    Path.Combine(builder.Environment.ContentRootPath, sp.GetRequiredService<IOptions<DemoOptions>>().Value.DemoFeedbackPath)));
builder.Services.AddScoped<IFeedbackGenerator>(sp =>
    AiProviderSelector.Choose(
        sp.GetRequiredService<IOptions<AiOptions>>().Value.Provider,
        sp.GetRequiredService<IOptions<ClaudeOptions>>().Value.IsConfigured,
        sp.GetRequiredService<IOptions<GeminiOptions>>().Value.IsConfigured) switch
    {
        AiProvider.Claude => sp.GetRequiredService<ClaudeFeedbackGenerator>(),
        AiProvider.Gemini => sp.GetRequiredService<GeminiFeedbackGenerator>(),
        _ => sp.GetRequiredService<DemoFeedbackGenerator>(),
    });

builder.Services.AddScoped<CoachingContextBuilder>();
builder.Services.AddScoped<CoachingService>();
builder.Services.AddScoped<TeamDashboardService>();
builder.Services.AddScoped<UserAuthService>();
builder.Services.AddScoped<DemoFeedbackExporter>();

// Email: the outbox is always on; delivery needs a provider (implement IEmailSender and register it here).
builder.Services.AddSingleton<IEmailSender, NoProviderEmailSender>();
builder.Services.AddScoped<FeedbackNotificationService>();
builder.Services.AddScoped<FeedbackCsvService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/access-denied";
        o.Cookie.Name = "wsc.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (exportDemo)
{
    await using var scope = app.Services.CreateAsyncScope();
    var live = scope.ServiceProvider.GetRequiredService<IFeedbackGenerator>();
    if (!live.IsLive)
    {
        Console.Error.WriteLine("No AI provider is configured (Claude:ApiKey or Gemini:ApiKey). See README → Live AI.");
        return;
    }

    var path = Path.Combine(app.Environment.ContentRootPath,
        scope.ServiceProvider.GetRequiredService<IOptions<DemoOptions>>().Value.DemoFeedbackPath);
    var count = await scope.ServiceProvider.GetRequiredService<DemoFeedbackExporter>().ExportAsync(live, path);
    Console.WriteLine($"Wrote live AI demo feedback ({live.ModelName}) for {count} reps to {path}");
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// A login form rendered for one user but submitted as another (stale tab) fails antiforgery validation.
// Send the person back to a fresh login page with an explanation instead of a raw 400.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (BadHttpRequestException ex) when (ex.InnerException is AntiforgeryValidationException && !context.Response.HasStarted)
    {
        context.Response.Redirect("/login?expired=1");
    }
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapExportEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
