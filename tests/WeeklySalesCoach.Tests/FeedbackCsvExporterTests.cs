using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Services.Export;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class FeedbackCsvExporterTests
{
    private static FeedbackCsvRow Row(string rep, string summary, ReportStatus status = ReportStatus.Approved)
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);
        content.Summary = summary;
        return new FeedbackCsvRow(rep, "rep@example.com", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18), status,
            new DateTime(2026, 9, 18, 22, 30, 0, DateTimeKind.Utc), "claude-haiku-4-5", content);
    }

    [Fact]
    public void Writes_a_header_and_one_line_per_report_with_excel_friendly_quoting()
    {
        var csv = FeedbackCsvExporter.Build([Row("Laura Bennett", "Steady week, \"solid\" follow-up.\nKeep going.")]);
        var lines = csv.Split("\r\n");

        Assert.StartsWith("﻿", csv); // UTF-8 BOM so Excel reads accents correctly
        Assert.Equal("Rep,Email,Week start,Week end,Status,Approved at (UTC),Model,Summary,Strengths,Improvements,Actions,Data gaps", lines[0].TrimStart('﻿'));
        Assert.Contains("\"Laura Bennett\",\"rep@example.com\",\"2026-09-14\",\"2026-09-18\",\"Approved\",\"2026-09-18 22:30\",\"claude-haiku-4-5\"", csv);
        Assert.Contains("\"Steady week, \"\"solid\"\" follow-up.\nKeep going.\"", csv); // quotes doubled, newline kept inside quotes
        Assert.Contains("Closed Delgado — Funded on schedule. (Evidence: Delgado Family mortgage, $95,000, funded 09-17.)", csv);
        Assert.Contains("Call every Documents Pending client by Tuesday — Files are stalling. (Success: No file older than 5 days in Documents Pending.)", csv);
    }

    [Fact]
    public void Separates_multiple_items_with_line_breaks_inside_the_cell()
    {
        var row = Row("Ana Torres", "s");
        row.Content.Strengths.Add(new FeedbackItem { Title = "Second", Detail = "d", Evidence = "e" });

        var csv = FeedbackCsvExporter.Build([row]);

        Assert.Contains("(Evidence: Delgado Family mortgage, $95,000, funded 09-17.)\nSecond — d. (Evidence: e)", csv);
    }

    [Fact]
    public void Draft_without_approval_date_leaves_the_cell_empty()
    {
        var row = Row("Ana Torres", "s", ReportStatus.Draft) with { ApprovedAtUtc = null };

        var csv = FeedbackCsvExporter.Build([row]);

        Assert.Contains("\"Draft\",\"\",\"claude-haiku-4-5\"", csv);
    }
}
