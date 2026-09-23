using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Tests.TestSupport;

public static class TestData
{
    public static Week AddWeek(AppDbContext db, DateOnly start, bool isCurrent)
    {
        var week = new Week { StartDate = start, EndDate = start.AddDays(4), IsCurrent = isCurrent };
        db.Weeks.Add(week);
        return week;
    }

    public static Rep AddRep(AppDbContext db, string name, decimal quota = 100_000m)
    {
        var rep = new Rep
        {
            Name = name,
            Title = "Loan Officer",
            TenureMonths = 12,
            WeeklyQuota = quota,
            PersonalGoal = "Close larger loans",
            FeedbackStyle = FeedbackStyle.Direct,
        };
        db.Reps.Add(rep);
        return rep;
    }

    public static WeeklyMetrics AddMetrics(AppDbContext db, Rep rep, Week week, int calls = 10,
        decimal wonVolume = 0m, int dealsWon = 0, int applications = 2, double responseHours = 4.0)
    {
        var metrics = new WeeklyMetrics
        {
            Rep = rep, Week = week,
            CallsMade = calls, CallsConnected = calls / 2,
            EmailsSent = 20, EmailsReplied = 4, NewLeads = 5,
            ApplicationsSubmitted = applications, DealsWon = dealsWon, WonVolume = wonVolume,
            DealsLost = 0, AvgLeadResponseHours = responseHours,
        };
        db.WeeklyMetrics.Add(metrics);
        return metrics;
    }

    public static Deal AddDeal(AppDbContext db, Rep rep, string client, string stage, decimal amount,
        DateOnly stageChangedOn, DateOnly lastContactOn, string notes = "")
    {
        var deal = new Deal
        {
            Rep = rep, ClientName = client, Product = "Mortgage", Amount = amount, Stage = stage,
            CreatedOn = stageChangedOn, StageChangedOn = stageChangedOn, LastContactOn = lastContactOn,
            Notes = notes,
        };
        db.Deals.Add(deal);
        return deal;
    }
}
