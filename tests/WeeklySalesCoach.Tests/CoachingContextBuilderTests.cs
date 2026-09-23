using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingContextBuilderTests
{
    private static readonly DateOnly PreviousStart = new(2026, 9, 7);
    private static readonly DateOnly CurrentStart = new(2026, 9, 14);

    private static CoachingContextBuilder Builder(TestDb t) => new(t.Factory, Options.Create(new DomainOptions
    {
        DealTerm = "Loan", RepTerm = "Loan Officer", WonStage = "Funded", LostStage = "Lost",
    }));

    [Fact]
    public async Task Includes_current_previous_and_team_average()
    {
        using var t = new TestDb();
        int anaId, weekId;
        using (var db = t.CreateContext())
        {
            var prev = TestData.AddWeek(db, PreviousStart, isCurrent: false);
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var ana = TestData.AddRep(db, "Ana Torres", quota: 200_000m);
            var carlos = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, ana, prev, calls: 20);
            TestData.AddMetrics(db, ana, cur, calls: 40, wonVolume: 150_000m);
            TestData.AddMetrics(db, carlos, cur, calls: 60);
            db.SaveChanges();
            anaId = ana.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(anaId, weekId);

        Assert.Equal("Ana Torres", ctx.Rep.Name);
        Assert.Equal(40, ctx.Current.CallsMade);
        Assert.Equal(20, ctx.Previous!.CallsMade);
        Assert.Equal(50, ctx.TeamAverage.CallsMade);
        Assert.Equal(75.0, ctx.QuotaAttainmentPercent);
        Assert.Equal("Loan", ctx.DealTerm);
        Assert.Equal(new DateOnly(2026, 9, 18), ctx.WeekEnd);
    }

    [Fact]
    public async Task Previous_is_null_when_there_is_no_previous_week()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Sofía Herrera");
            TestData.AddMetrics(db, rep, cur);
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Null(ctx.Previous);
    }

    [Fact]
    public async Task Quota_attainment_is_zero_when_quota_is_zero()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Laura Bennett", quota: 0m);
            TestData.AddMetrics(db, rep, cur, wonVolume: 50_000m);
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Equal(0, ctx.QuotaAttainmentPercent);
    }

    [Fact]
    public async Task Deals_show_days_relative_to_week_end_with_open_deals_first()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, rep, cur);
            TestData.AddDeal(db, rep, "Big Funded", "Funded", 500_000m, new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17));
            TestData.AddDeal(db, rep, "Summit Auto Repair", "Documents Pending", 120_000m, new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9));
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Equal("Summit Auto Repair", ctx.Deals[0].Client);
        Assert.Equal(10, ctx.Deals[0].DaysInStage);
        Assert.Equal(9, ctx.Deals[0].DaysSinceLastContact);
        Assert.Equal("Big Funded", ctx.Deals[1].Client);
    }

    [Fact]
    public async Task Includes_trimmed_manager_note()
    {
        using var t = new TestDb();
        int repId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            var rep = TestData.AddRep(db, "Miguel Ortega");
            TestData.AddMetrics(db, rep, cur);
            db.CoachingReports.Add(new WeeklySalesCoach.Data.CoachingReport { Rep = rep, Week = cur, ManagerNote = "  Out 2 days for training.  " });
            db.SaveChanges();
            repId = rep.Id; weekId = cur.Id;
        }

        var ctx = await Builder(t).BuildAsync(repId, weekId);

        Assert.Equal("Out 2 days for training.", ctx.ManagerNote);
    }

    [Fact]
    public async Task Throws_for_unknown_rep()
    {
        using var t = new TestDb();
        int weekId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, CurrentStart, isCurrent: true);
            db.SaveChanges();
            weekId = week.Id;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => Builder(t).BuildAsync(999, weekId));
    }
}
