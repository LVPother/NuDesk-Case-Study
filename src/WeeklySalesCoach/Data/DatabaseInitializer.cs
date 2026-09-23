using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Services.Notifications;

namespace WeeklySalesCoach.Data;

public static class DatabaseInitializer
{
    /// <summary>The rep whose report is pre-approved on first run, so the rep view has something to show.</summary>
    public const string PreApprovedRep = "Laura Bennett";

    /// <summary>Creates the SQLite schema (no migrations needed for the demo) and seeds it on first run.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        var demo = scope.ServiceProvider.GetRequiredService<DemoFeedbackGenerator>();

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await SeedData.SeedAsync(db, hasher, ct);
        await SeedData.SeedHistoryAsync(db, ct);

        if (demo.TryGetRaw(PreApprovedRep, out var raw))
        {
            // Validate through the same parser the app uses, and store it normalized.
            var content = CoachingContentParser.Parse(raw);
            var approvedAt = new DateTime(2026, 9, 18, 22, 30, 0, DateTimeKind.Utc); // end of the demo week
            await SeedData.ApproveDemoReportAsync(db, PreApprovedRep, CoachingContentParser.Serialize(content), demo.ModelName, approvedAt, ct);

            // Same side effect an approval in the UI has: the email lands in the outbox.
            var approvedReport = await db.CoachingReports.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Rep!.Name == PreApprovedRep && r.Week!.IsCurrent && r.Status == ReportStatus.Approved, ct);
            if (approvedReport is not null)
                await scope.ServiceProvider.GetRequiredService<FeedbackNotificationService>().QueueApprovedReportAsync(approvedReport.Id, ct);
        }
    }
}
