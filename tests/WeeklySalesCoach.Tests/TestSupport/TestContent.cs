namespace WeeklySalesCoach.Tests.TestSupport;

public static partial class TestContent
{
    public const string ValidJson = """
        {
          "summary": "Solid week.",
          "strengths": [
            { "title": "Closed Delgado", "detail": "Funded on schedule.", "evidence": "Delgado Family mortgage, $95,000, funded 09-17." }
          ],
          "improvements": [
            { "title": "Document follow-up", "detail": "Three files stalled.", "evidence": "Summit Auto Repair: 10 days in Documents Pending." }
          ],
          "actions": [
            { "action": "Call every Documents Pending client by Tuesday", "why": "Files are stalling.", "successSignal": "No file older than 5 days in Documents Pending." }
          ],
          "dataGaps": []
        }
        """;
}

public static partial class TestContent
{
    public static WeeklySalesCoach.Services.Coaching.CoachingContext SampleContext(string name = "Ana Torres") => new(
        RepId: 1, WeekId: 1,
        WeekStart: new DateOnly(2026, 9, 14), WeekEnd: new DateOnly(2026, 9, 18),
        DealTerm: "Loan", RepTerm: "Loan Officer",
        Rep: new WeeklySalesCoach.Services.Coaching.RepProfile(name, "Loan Officer", 12, 100_000m, "Close larger loans", WeeklySalesCoach.Data.FeedbackStyle.Direct),
        Current: Snapshot(40), Previous: Snapshot(30), TeamAverage: Snapshot(35),
        QuotaAttainmentPercent: 80,
        Deals: [new WeeklySalesCoach.Services.Coaching.DealSnapshot("Hartwell Family", "Mortgage", 210_000m, "Funded", 2, 2, "Funded after follow-up.", null)],
        ManagerNote: null);

    private static WeeklySalesCoach.Services.Coaching.MetricsSnapshot Snapshot(double calls) =>
        new(calls, calls / 2, 50, 10, 5, 3, 1, 50_000m, 0, 4);
}
