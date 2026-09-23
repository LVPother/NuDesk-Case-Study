using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class AppDbContextTests
{
    [Fact]
    public void Round_trips_a_report_with_enum_values()
    {
        using var t = new TestDb();
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var rep = TestData.AddRep(db, "Ana Torres");
            db.CoachingReports.Add(new CoachingReport
            {
                Rep = rep, Week = week, Status = ReportStatus.Draft, Source = ReportSource.Demo,
            });
            db.SaveChanges();
        }

        using var check = t.CreateContext();
        var report = check.CoachingReports.Single();
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(ReportSource.Demo, report.Source);
    }

    [Fact]
    public void Allows_only_one_report_per_rep_and_week()
    {
        using var t = new TestDb();
        using var db = t.CreateContext();
        var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
        var rep = TestData.AddRep(db, "Ana Torres");
        db.CoachingReports.Add(new CoachingReport { Rep = rep, Week = week });
        db.SaveChanges();

        db.CoachingReports.Add(new CoachingReport { Rep = rep, Week = week });
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }
}
