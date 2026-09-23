using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Services.Coaching;

public class CoachingContextBuilder(IDbContextFactory<AppDbContext> dbFactory, IOptions<DomainOptions> domain)
{
    public async Task<CoachingContext> BuildAsync(int repId, int weekId, CancellationToken ct = default)
    {
        var terms = domain.Value;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var week = await db.Weeks.AsNoTracking().SingleAsync(w => w.Id == weekId, ct);
        var rep = await db.Reps.AsNoTracking().SingleAsync(r => r.Id == repId, ct);
        var weekMetrics = await db.WeeklyMetrics.AsNoTracking().Where(m => m.WeekId == weekId).ToListAsync(ct);
        var current = weekMetrics.SingleOrDefault(m => m.RepId == repId)
            ?? throw new InvalidOperationException($"No metrics for rep {repId} in week {weekId}.");

        var previousWeek = await db.Weeks.AsNoTracking()
            .Where(w => w.EndDate < week.StartDate)
            .OrderByDescending(w => w.EndDate)
            .FirstOrDefaultAsync(ct);
        var previous = previousWeek is null
            ? null
            : await db.WeeklyMetrics.AsNoTracking().SingleOrDefaultAsync(m => m.RepId == repId && m.WeekId == previousWeek.Id, ct);

        var deals = await db.Deals.AsNoTracking().Where(d => d.RepId == repId).ToListAsync(ct);
        var note = await db.CoachingReports.AsNoTracking()
            .Where(r => r.RepId == repId && r.WeekId == weekId)
            .Select(r => r.ManagerNote)
            .SingleOrDefaultAsync(ct);

        return new CoachingContext(
            RepId: rep.Id,
            WeekId: week.Id,
            WeekStart: week.StartDate,
            WeekEnd: week.EndDate,
            DealTerm: terms.DealTerm,
            RepTerm: terms.RepTerm,
            Rep: new RepProfile(rep.Name, rep.Title, rep.TenureMonths, rep.WeeklyQuota, rep.PersonalGoal, rep.FeedbackStyle),
            Current: ToSnapshot(current),
            Previous: previous is null ? null : ToSnapshot(previous),
            TeamAverage: Average(weekMetrics),
            QuotaAttainmentPercent: Calc.Attainment(current.WonVolume, rep.WeeklyQuota),
            Deals: deals
                .OrderBy(d => d.Stage == terms.WonStage || d.Stage == terms.LostStage ? 1 : 0)
                .ThenByDescending(d => d.Amount)
                .Select(d => new DealSnapshot(
                    d.ClientName, d.Product, d.Amount, d.Stage,
                    Calc.DaysBetween(d.StageChangedOn, week.EndDate),
                    Calc.DaysBetween(d.LastContactOn, week.EndDate),
                    d.Notes, d.LostReason))
                .ToList(),
            ManagerNote: string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    private static MetricsSnapshot ToSnapshot(WeeklyMetrics m) => new(
        m.CallsMade, m.CallsConnected, m.EmailsSent, m.EmailsReplied, m.NewLeads,
        m.ApplicationsSubmitted, m.DealsWon, m.WonVolume, m.DealsLost, m.AvgLeadResponseHours);

    /// <summary>Team average, aggregate only: no individual names or rows leave this method.</summary>
    private static MetricsSnapshot Average(IReadOnlyCollection<WeeklyMetrics> all) => new(
        R(all.Average(m => m.CallsMade)),
        R(all.Average(m => m.CallsConnected)),
        R(all.Average(m => m.EmailsSent)),
        R(all.Average(m => m.EmailsReplied)),
        R(all.Average(m => m.NewLeads)),
        R(all.Average(m => m.ApplicationsSubmitted)),
        R(all.Average(m => m.DealsWon)),
        Math.Round(all.Average(m => m.WonVolume), 0),
        R(all.Average(m => m.DealsLost)),
        R(all.Average(m => m.AvgLeadResponseHours)));

    private static double R(double value) => Math.Round(value, 1);
}
