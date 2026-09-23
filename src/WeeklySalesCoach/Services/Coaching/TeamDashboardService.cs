using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;

namespace WeeklySalesCoach.Services.Coaching;

public record TeamKpis(decimal WonVolume, decimal TeamQuota, int DealsWon, int Applications, double AvgLeadResponseHours);

public record RepCard(int RepId, string Name, string Title, string Initials, decimal WonVolume, decimal Quota,
    double AttainmentPercent, ReportStatus Status, bool IsEdited);

public record TeamDashboard(Week Week, TeamKpis Kpis, IReadOnlyList<RepCard> Reps);

public class TeamDashboardService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<TeamDashboard> GetAsync(int weekId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var week = await db.Weeks.AsNoTracking().SingleAsync(w => w.Id == weekId, ct);
        var reps = await db.Reps.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var metrics = await db.WeeklyMetrics.AsNoTracking().Where(m => m.WeekId == weekId).ToDictionaryAsync(m => m.RepId, ct);
        var reports = await db.CoachingReports.AsNoTracking().Where(r => r.WeekId == weekId).ToDictionaryAsync(r => r.RepId, ct);

        var cards = reps.Select(rep =>
        {
            metrics.TryGetValue(rep.Id, out var m);
            reports.TryGetValue(rep.Id, out var report);
            var won = m?.WonVolume ?? 0m;
            return new RepCard(rep.Id, rep.Name, rep.Title, Initials(rep.Name), won, rep.WeeklyQuota,
                Calc.Attainment(won, rep.WeeklyQuota), report?.Status ?? ReportStatus.NotGenerated, report?.IsEdited ?? false);
        }).ToList();

        var all = metrics.Values.ToList();
        var kpis = new TeamKpis(
            all.Sum(m => m.WonVolume),
            reps.Sum(r => r.WeeklyQuota),
            all.Sum(m => m.DealsWon),
            all.Sum(m => m.ApplicationsSubmitted),
            all.Count == 0 ? 0 : Math.Round(all.Average(m => m.AvgLeadResponseHours), 1));

        return new TeamDashboard(week, kpis, cards);
    }

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
