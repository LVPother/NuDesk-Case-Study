using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;

namespace WeeklySalesCoach.Services.Coaching;

public record ReportHistoryEntry(Week Week, CoachingReport Report);

public record TeamGenerationFailure(string RepName, string Message);

/// <summary>Outcome of a whole-team run: who got a draft, who was skipped (approved/edited), who failed and why.</summary>
public record TeamGenerationResult(
    IReadOnlyList<string> Generated,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<TeamGenerationFailure> Failed,
    bool StoppedEarly);

/// <summary>Report lifecycle: NotGenerated → Draft (AI) → edited by manager → Approved (visible to the rep) → reopened as Draft when revised.</summary>
public class CoachingService(
    IDbContextFactory<AppDbContext> dbFactory,
    CoachingContextBuilder contextBuilder,
    IFeedbackGenerator generator,
    TimeProvider time,
    ILogger<CoachingService> logger)
{
    public const int ManagerNoteMaxLength = 1000;

    public bool IsLiveAi => generator.IsLive;

    public async Task<Week> GetCurrentWeekAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Weeks.AsNoTracking().SingleAsync(w => w.IsCurrent, ct);
    }

    public async Task<string?> GetRepNameAsync(int repId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Reps.AsNoTracking().Where(r => r.Id == repId).Select(r => r.Name).SingleOrDefaultAsync(ct);
    }

    public async Task<CoachingReport> GetOrCreateReportAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await GetOrCreateAsync(db, repId, weekId, ct);
    }

    public async Task SaveManagerNoteAsync(int repId, int weekId, string? note, CancellationToken ct = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > ManagerNoteMaxLength)
            throw new ArgumentException($"The manager note must be {ManagerNoteMaxLength} characters or fewer.", nameof(note));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await GetOrCreateAsync(db, repId, weekId, ct);
        EnsureNotApproved(report);
        report.ManagerNote = trimmed;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CoachingReport> GenerateAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            EnsureNotApproved(await GetOrCreateAsync(db, repId, weekId, ct));
        }

        var context = await contextBuilder.BuildAsync(repId, weekId, ct);
        var content = await GenerateValidContentAsync(context, ct);

        await using var save = await dbFactory.CreateDbContextAsync(ct);
        var report = await save.CoachingReports.SingleAsync(r => r.RepId == repId && r.WeekId == weekId, ct);
        EnsureNotApproved(report);
        report.ContentJson = CoachingContentParser.Serialize(content);
        report.Status = ReportStatus.Draft;
        report.Source = generator.IsLive ? ReportSource.Live : ReportSource.Demo;
        report.ModelName = generator.ModelName;
        report.IsEdited = false;
        report.GeneratedAtUtc = time.GetUtcNow().UtcDateTime;
        await save.SaveChangesAsync(ct);
        return report;
    }

    /// <summary>
    /// Bulk generation: re-reads the report right before generating, so reports approved or hand-edited
    /// (e.g. in another tab) since the caller loaded its data are skipped instead of overwritten.
    /// Returns false when the report was skipped.
    /// </summary>
    public async Task<bool> GenerateIfEligibleAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var report = await GetOrCreateAsync(db, repId, weekId, ct);
            if (report.Status == ReportStatus.Approved || report.IsEdited) return false;
        }

        await GenerateAsync(repId, weekId, ct);
        return true;
    }

    /// <summary>
    /// Generates drafts for every rep in the week, one at a time. A rep whose output is invalid twice is
    /// reported and the run continues; a rate limit, missing key or rejected request stops the run, since
    /// the next reps would fail the same way.
    /// </summary>
    public async Task<TeamGenerationResult> GenerateTeamAsync(int weekId, Action<string> progress, CancellationToken ct = default)
    {
        List<Rep> reps;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            reps = await db.Reps.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        }

        var generated = new List<string>();
        var skipped = new List<string>();
        var failed = new List<TeamGenerationFailure>();
        var stoppedEarly = false;

        foreach (var rep in reps)
        {
            await using (var db = await dbFactory.CreateDbContextAsync(ct))
            {
                var report = await GetOrCreateAsync(db, rep.Id, weekId, ct);
                if (report.Status == ReportStatus.Approved || report.IsEdited)
                {
                    skipped.Add(rep.Name);
                    continue;
                }
            }

            progress(rep.Name);
            try
            {
                await GenerateAsync(rep.Id, weekId, ct);
                generated.Add(rep.Name);
            }
            catch (InvalidCoachingContentException ex)
            {
                failed.Add(new TeamGenerationFailure(rep.Name, AiErrorMessages.For(ex)));
            }
            catch (InvalidOperationException)
            {
                skipped.Add(rep.Name); // approved in another tab while we were running
            }
            catch (AiUnavailableException ex)
            {
                failed.Add(new TeamGenerationFailure(rep.Name, AiErrorMessages.For(ex)));
                if (ex.Kind is AiFailureKind.RateLimited or AiFailureKind.NotConfigured or AiFailureKind.InvalidRequest)
                {
                    stoppedEarly = true;
                    break;
                }
            }
        }

        return new TeamGenerationResult(generated, skipped, failed, stoppedEarly);
    }

    /// <summary>Saves the manager's edits. Returns false when the content is unchanged (nothing to save).</summary>
    public async Task<bool> SaveEditsAsync(int reportId, CoachingContent content, CancellationToken ct = default)
    {
        CoachingContentParser.Validate(content);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.CoachingReports.SingleAsync(r => r.Id == reportId, ct);
        EnsureNotApproved(report);
        if (report.Status != ReportStatus.Draft)
            throw new InvalidOperationException("Generate a draft before editing.");

        var json = CoachingContentParser.Serialize(content);
        if (json == report.ContentJson) return false;

        report.ContentJson = json;
        report.IsEdited = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Every week that has a report for this rep, newest first (manager view: any status).</summary>
    public Task<IReadOnlyList<ReportHistoryEntry>> GetHistoryAsync(int repId, CancellationToken ct = default) =>
        LoadHistoryAsync(repId, approvedOnly: false, ct);

    /// <summary>What the rep may browse: only approved weeks, newest first.</summary>
    public Task<IReadOnlyList<ReportHistoryEntry>> GetApprovedHistoryAsync(int repId, CancellationToken ct = default) =>
        LoadHistoryAsync(repId, approvedOnly: true, ct);

    private async Task<IReadOnlyList<ReportHistoryEntry>> LoadHistoryAsync(int repId, bool approvedOnly, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.CoachingReports.AsNoTracking().Include(r => r.Week).Where(r => r.RepId == repId);
        if (approvedOnly) query = query.Where(r => r.Status == ReportStatus.Approved);

        var reports = await query.OrderByDescending(r => r.Week!.StartDate).ToListAsync(ct);
        return reports.Select(r => new ReportHistoryEntry(r.Week!, r)).ToList();
    }

    public async Task ApproveAsync(int reportId, int approvedByUserId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.CoachingReports.SingleAsync(r => r.Id == reportId, ct);
        EnsureNotApproved(report);
        if (report.Status != ReportStatus.Draft)
            throw new InvalidOperationException("Only a draft can be approved.");

        report.Status = ReportStatus.Approved;
        report.ApprovedAtUtc = time.GetUtcNow().UtcDateTime;
        report.ApprovedByUserId = approvedByUserId;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reopens an approved report as a draft so the manager can revise and share it again. The rep stops seeing it
    /// until it is approved once more; the earlier approval stays on record through the email outbox.
    /// </summary>
    public async Task ReopenAsync(int reportId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var report = await db.CoachingReports.SingleAsync(r => r.Id == reportId, ct);
        if (report.Status != ReportStatus.Approved)
            throw new InvalidOperationException("Only an approved report can be reopened.");

        report.Status = ReportStatus.Draft;
        report.IsEdited = true; // protects the text from being overwritten by a team-wide generation run
        report.ApprovedAtUtc = null;
        report.ApprovedByUserId = null;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>What a rep may see: only their own report, only once approved.</summary>
    public async Task<CoachingReport?> GetApprovedForRepAsync(int repId, int weekId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CoachingReports.AsNoTracking()
            .SingleOrDefaultAsync(r => r.RepId == repId && r.WeekId == weekId && r.Status == ReportStatus.Approved, ct);
    }

    public static CoachingContent? ReadContent(CoachingReport report) =>
        report.ContentJson is null ? null : CoachingContentParser.FromStored(report.ContentJson);

    private async Task<CoachingContent> GenerateValidContentAsync(CoachingContext context, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return CoachingContentParser.Parse(await generator.GenerateAsync(context, ct));
            }
            catch (InvalidCoachingContentException ex) when (attempt == 1)
            {
                logger.LogWarning(ex, "AI returned invalid content for rep {RepId}; retrying once.", context.RepId);
            }
        }
    }

    private static async Task<CoachingReport> GetOrCreateAsync(AppDbContext db, int repId, int weekId, CancellationToken ct)
    {
        var report = await db.CoachingReports.SingleOrDefaultAsync(r => r.RepId == repId && r.WeekId == weekId, ct);
        if (report is not null) return report;

        report = new CoachingReport { RepId = repId, WeekId = weekId, Status = ReportStatus.NotGenerated };
        db.CoachingReports.Add(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    private static void EnsureNotApproved(CoachingReport report)
    {
        if (report.Status == ReportStatus.Approved)
            throw new InvalidOperationException("This report is already approved and can no longer be changed.");
    }
}
