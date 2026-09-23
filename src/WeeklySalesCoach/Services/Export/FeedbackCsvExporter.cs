using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.Export;

public record FeedbackCsvRow(
    string RepName,
    string Email,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    ReportStatus Status,
    DateTime? ApprovedAtUtc,
    string? Model,
    CoachingContent Content);

/// <summary>Flattens coaching reports into a CSV that opens cleanly in Excel / Google Sheets (UTF-8 BOM, CRLF, quoted cells).</summary>
public static class FeedbackCsvExporter
{
    private static readonly string[] Header =
    [
        "Rep", "Email", "Week start", "Week end", "Status", "Approved at (UTC)", "Model",
        "Summary", "Strengths", "Improvements", "Actions", "Data gaps",
    ];

    public static string Build(IEnumerable<FeedbackCsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.Append(string.Join(",", Header)).Append("\r\n");

        foreach (var r in rows)
        {
            var cells = new[]
            {
                r.RepName,
                r.Email,
                r.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.WeekEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.Status.ToString(),
                r.ApprovedAtUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                r.Model ?? "",
                r.Content.Summary,
                string.Join("\n", r.Content.Strengths.Select(i => $"{i.Title} — {Sentence(i.Detail)} (Evidence: {i.Evidence})")),
                string.Join("\n", r.Content.Improvements.Select(i => $"{i.Title} — {Sentence(i.Detail)} (Evidence: {i.Evidence})")),
                string.Join("\n", r.Content.Actions.Select(a => $"{a.Action} — {Sentence(a.Why)} (Success: {a.SuccessSignal})")),
                string.Join("\n", r.Content.DataGaps),
            };
            sb.Append(string.Join(",", cells.Select(Quote))).Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Sentence(string text)
    {
        var t = (text ?? "").Trim();
        return t.Length == 0 || ".!?".Contains(t[^1]) ? t : t + ".";
    }

    private static string Quote(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}

/// <summary>Loads the rows for the two exports: everything (manager) or one rep's approved reports (rep).</summary>
public class FeedbackCsvService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<string> BuildForManagerAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var reports = await db.CoachingReports.AsNoTracking().Include(r => r.Rep).Include(r => r.Week)
            .Where(r => r.ContentJson != null)
            .OrderBy(r => r.Rep!.Name).ThenByDescending(r => r.Week!.StartDate)
            .ToListAsync(ct);
        return FeedbackCsvExporter.Build(reports.Select(ToRow));
    }

    public async Task<string> BuildForRepAsync(int repId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var reports = await db.CoachingReports.AsNoTracking().Include(r => r.Rep).Include(r => r.Week)
            .Where(r => r.RepId == repId && r.Status == ReportStatus.Approved && r.ContentJson != null)
            .OrderByDescending(r => r.Week!.StartDate)
            .ToListAsync(ct);
        return FeedbackCsvExporter.Build(reports.Select(ToRow));
    }

    private static FeedbackCsvRow ToRow(CoachingReport r) => new(
        r.Rep!.Name, r.Rep.Email, r.Week!.StartDate, r.Week.EndDate, r.Status, r.ApprovedAtUtc, r.ModelName,
        CoachingContentParser.FromStored(r.ContentJson!));
}
