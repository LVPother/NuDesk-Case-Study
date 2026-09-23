using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class SeedDataTests
{
    private static async Task<TestDb> SeededAsync()
    {
        var t = new TestDb();
        await using var db = t.CreateContext();
        await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        return t;
    }

    [Fact]
    public async Task Seeds_the_demo_team_exactly_once()
    {
        using var t = await SeededAsync();
        await using (var again = t.CreateContext())
        {
            await SeedData.SeedAsync(again, new PasswordHasher<AppUser>());
        }

        await using var db = t.CreateContext();
        Assert.Equal(5, await db.Reps.CountAsync());
        Assert.Equal(6, await db.Users.CountAsync());
        Assert.Equal(2, await db.Weeks.CountAsync());
        Assert.Equal(1, await db.Weeks.CountAsync(w => w.IsCurrent));
        Assert.Equal(10, await db.WeeklyMetrics.CountAsync());
        var miguelReport = await db.CoachingReports.Include(r => r.Rep).SingleAsync();
        Assert.Equal("Miguel Ortega", miguelReport.Rep!.Name);
        Assert.Equal("Out 2 days this week for compliance training.", miguelReport.ManagerNote);
    }

    [Fact]
    public async Task Every_rep_has_an_email_address()
    {
        using var t = await SeededAsync();
        await using var db = t.CreateContext();

        var reps = await db.Reps.ToListAsync();

        Assert.All(reps, r => Assert.Contains("@", r.Email));
        Assert.Equal(reps.Count, reps.Select(r => r.Email).Distinct().Count());
    }

    [Fact]
    public async Task ApproveDemoReport_pre_approves_one_rep_with_the_demo_feedback_once()
    {
        using var t = await SeededAsync();
        var approvedAt = new DateTime(2026, 9, 18, 22, 30, 0, DateTimeKind.Utc);
        await using (var db = t.CreateContext())
        {
            await SeedData.ApproveDemoReportAsync(db, "Laura Bennett", TestContent.ValidJson, "pre-generated demo", approvedAt);
            await SeedData.ApproveDemoReportAsync(db, "Laura Bennett", TestContent.ValidJson, "pre-generated demo", approvedAt);
        }

        await using var check = t.CreateContext();
        var laura = await check.CoachingReports.Include(r => r.Rep).Include(r => r.Week)
            .SingleAsync(r => r.Rep!.Name == "Laura Bennett");
        var roberto = await check.Users.SingleAsync(u => u.Username == "roberto");
        Assert.Equal(2, await check.CoachingReports.CountAsync());
        Assert.True(laura.Week!.IsCurrent);
        Assert.Equal(ReportStatus.Approved, laura.Status);
        Assert.Equal(ReportSource.Demo, laura.Source);
        Assert.Equal(roberto.Id, laura.ApprovedByUserId);
        Assert.Equal(approvedAt, laura.ApprovedAtUtc);
        Assert.Equal("Solid week.", Services.Coaching.CoachingService.ReadContent(laura)!.Summary);
    }

    [Fact]
    public async Task SeedHistory_adds_an_approved_previous_week_report_for_laura_once()
    {
        using var t = await SeededAsync();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedHistoryAsync(db);
            await SeedData.SeedHistoryAsync(db);
        }

        await using var check = t.CreateContext();
        var history = await check.CoachingReports.Include(r => r.Rep).Include(r => r.Week)
            .Where(r => r.Rep!.Name == "Laura Bennett" && !r.Week!.IsCurrent).ToListAsync();
        var report = Assert.Single(history);
        Assert.Equal(new DateOnly(2026, 9, 7), report.Week!.StartDate);
        Assert.Equal(ReportStatus.Approved, report.Status);
        Assert.Equal(ReportSource.Demo, report.Source);
        var content = Services.Coaching.CoachingService.ReadContent(report)!;
        Services.Coaching.CoachingContentParser.Validate(content);
        Assert.Contains("$79,000", content.Strengths[0].Evidence);
    }

    [Fact]
    public async Task Current_week_metrics_match_the_deal_pipeline()
    {
        using var t = await SeededAsync();
        await using var db = t.CreateContext();
        var week = await db.Weeks.SingleAsync(w => w.IsCurrent);
        var metrics = await db.WeeklyMetrics.Where(m => m.WeekId == week.Id).ToListAsync();
        var deals = await db.Deals.ToListAsync();

        foreach (var m in metrics)
        {
            var closedThisWeek = deals
                .Where(d => d.RepId == m.RepId && d.StageChangedOn >= week.StartDate && d.StageChangedOn <= week.EndDate)
                .ToList();
            var funded = closedThisWeek.Where(d => d.Stage == "Funded").ToList();
            Assert.Equal(m.DealsWon, funded.Count);
            Assert.Equal(m.WonVolume, funded.Sum(d => d.Amount));
            Assert.Equal(m.DealsLost, closedThisWeek.Count(d => d.Stage == "Lost"));
        }
    }
}
