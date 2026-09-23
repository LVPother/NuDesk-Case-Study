using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class TeamDashboardServiceTests
{
    [Fact]
    public async Task Computes_team_kpis_and_rep_cards()
    {
        using var t = new TestDb();
        int weekId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var ana = TestData.AddRep(db, "Ana Torres", quota: 200_000m);
            var carlos = TestData.AddRep(db, "Carlos Mendoza", quota: 100_000m);
            TestData.AddMetrics(db, ana, week, wonVolume: 150_000m, dealsWon: 2, applications: 3, responseHours: 4.0);
            TestData.AddMetrics(db, carlos, week, wonVolume: 50_000m, dealsWon: 1, applications: 5, responseHours: 2.0);
            db.CoachingReports.Add(new CoachingReport { Rep = ana, Week = week, Status = ReportStatus.Draft, IsEdited = true });
            db.SaveChanges();
            weekId = week.Id;
        }

        var dashboard = await new TeamDashboardService(t.Factory).GetAsync(weekId);

        Assert.Equal(200_000m, dashboard.Kpis.WonVolume);
        Assert.Equal(300_000m, dashboard.Kpis.TeamQuota);
        Assert.Equal(3, dashboard.Kpis.DealsWon);
        Assert.Equal(8, dashboard.Kpis.Applications);
        Assert.Equal(3.0, dashboard.Kpis.AvgLeadResponseHours);

        var anaCard = dashboard.Reps[0];
        Assert.Equal("Ana Torres", anaCard.Name);
        Assert.Equal("AT", anaCard.Initials);
        Assert.Equal(75.0, anaCard.AttainmentPercent);
        Assert.Equal(ReportStatus.Draft, anaCard.Status);
        Assert.True(anaCard.IsEdited);
        Assert.Equal(ReportStatus.NotGenerated, dashboard.Reps[1].Status);
    }

    [Fact]
    public async Task Attainment_is_zero_for_zero_quota()
    {
        using var t = new TestDb();
        int weekId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var rep = TestData.AddRep(db, "Laura Bennett", quota: 0m);
            TestData.AddMetrics(db, rep, week, wonVolume: 10_000m);
            db.SaveChanges();
            weekId = week.Id;
        }

        var dashboard = await new TeamDashboardService(t.Factory).GetAsync(weekId);

        Assert.Equal(0, dashboard.Reps[0].AttainmentPercent);
    }

    [Theory]
    [InlineData("Sofía Herrera", "SH")]
    [InlineData("Cher", "C")]
    [InlineData("Ana María Torres", "AM")]
    public void Initials_use_the_first_two_words(string name, string expected)
    {
        Assert.Equal(expected, TeamDashboardService.Initials(name));
    }
}
