using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WeeklySalesCoach.Data;

/// <summary>
/// Fictional loan-origination team. Each rep is designed to exercise a different coaching skill:
/// Ana (recognize a top performer), Carlos (bottleneck), Sofía (encourage a new hire),
/// Miguel (respect manager context), Laura (align with a personal goal).
/// </summary>
public static class SeedData
{
    public const string DemoPassword = "demo123";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken ct = default)
    {
        if (await db.Reps.AnyAsync(ct)) return;

        var previous = new Week { StartDate = new DateOnly(2026, 9, 7), EndDate = new DateOnly(2026, 9, 11), IsCurrent = false };
        var current = new Week { StartDate = new DateOnly(2026, 9, 14), EndDate = new DateOnly(2026, 9, 18), IsCurrent = true };
        db.Weeks.AddRange(previous, current);

        var ana = NewRep("Ana Torres", "ana.torres@example.com", "Senior Loan Officer", 48, 250_000m, "Mentor newer officers while keeping volume above quota", FeedbackStyle.Direct);
        var carlos = NewRep("Carlos Mendoza", "carlos.mendoza@example.com", "Loan Officer", 12, 150_000m, "Improve application-to-funding conversion", FeedbackStyle.Direct);
        var sofia = NewRep("Sofía Herrera", "sofia.herrera@example.com", "Junior Loan Officer", 2, 80_000m, "Close my first loans and build a steady pipeline", FeedbackStyle.Encouraging);
        var miguel = NewRep("Miguel Ortega", "miguel.ortega@example.com", "Senior Loan Officer", 84, 200_000m, "Win more commercial (SBA) clients", FeedbackStyle.Direct);
        var laura = NewRep("Laura Bennett", "laura.bennett@example.com", "Loan Officer", 24, 150_000m, "Close larger loans", FeedbackStyle.Encouraging);
        db.Reps.AddRange(ana, carlos, sofia, miguel, laura);

        db.Users.Add(User("roberto", "Roberto Díaz", UserRole.Manager, null));
        db.Users.AddRange(
            User("ana", ana.Name, UserRole.Rep, ana),
            User("carlos", carlos.Name, UserRole.Rep, carlos),
            User("sofia", sofia.Name, UserRole.Rep, sofia),
            User("miguel", miguel.Name, UserRole.Rep, miguel),
            User("laura", laura.Name, UserRole.Rep, laura));

        //                    calls conn emails repl leads apps won volume   lost resp(h)
        db.WeeklyMetrics.AddRange(
            M(ana, previous,     40,  17,  48,  15,  8,   5,  2, 268_000m, 1, 8.8),
            M(ana, current,      38,  16,  45,  14,  9,   6,  3, 312_000m, 1, 9.5),
            M(carlos, previous,  65,  19,  85,   8, 11,   6,  1, 110_000m, 3, 3.4),
            M(carlos, current,   72,  21,  90,   9, 12,   7,  1,  95_000m, 2, 3.1),
            M(sofia, previous,   28,   7,  35,   5,  4,   1,  0,       0m, 1, 6.5),
            M(sofia, current,    40,  12,  50,  10,  6,   3,  1,  62_000m, 0, 4.0),
            M(miguel, previous,  45,  18,  55,  15,  8,   5,  3, 410_000m, 0, 4.2),
            M(miguel, current,   22,   9,  30,   8,  5,   2,  1, 140_000m, 1, 7.0),
            M(laura, previous,   33,  14,  58,  20,  5,   3,  2,  79_000m, 0, 2.2),
            M(laura, current,    35,  15,  60,  22,  5,   4,  2,  88_000m, 0, 2.0));

        // Day numbers are days of September 2026: created, stage changed, last contact.
        db.Deals.AddRange(
            D(ana, "Hartwell Family", "Mortgage", 210_000m, "Funded", 2, 16, 16, "Funded after rate-lock follow-ups on 09-12 and 09-15."),
            D(ana, "M. Castillo", "Refinance", 72_000m, "Funded", 3, 15, 15, "Smooth file; all documents on first request."),
            D(ana, "J. Rivera", "Auto Loan", 30_000m, "Funded", 9, 17, 17, "Same-week approval and funding."),
            D(ana, "Brightside Bakery LLC", "SBA Loan", 180_000m, "Underwriting", 1, 15, 17, "Underwriter requested 2025 tax returns; client sending Monday."),
            D(ana, "T. Nguyen", "Auto Loan", 28_000m, "Lead", 14, 14, 16, "Inbound web lead; first call two days after inquiry."),
            D(ana, "J. Okafor", "Personal Loan", 12_000m, "Lead", 10, 10, 10, "Inbound web lead. Not contacted yet."),
            D(ana, "Pinecrest Dental", "Refinance", 350_000m, "Lost", 1, 17, 17, "Negotiated for two weeks.", "Competitor offered a 0.25% lower rate."),

            D(carlos, "Delgado Family", "Mortgage", 95_000m, "Funded", 1, 17, 17, "Funded on schedule."),
            D(carlos, "Summit Auto Repair", "SBA Loan", 120_000m, "Documents Pending", 2, 8, 9, "Waiting on 3 months of bank statements."),
            D(carlos, "K. Patel", "Refinance", 240_000m, "Documents Pending", 4, 10, 10, "Requested pay stubs and W-2s."),
            D(carlos, "L. Moreno", "Personal Loan", 15_000m, "Documents Pending", 8, 11, 11, "Requested proof of income."),
            D(carlos, "B. Foster", "Mortgage", 310_000m, "Application", 15, 17, 17, "Application submitted; pre-approval pending."),
            D(carlos, "Greenway Landscaping", "SBA Loan", 60_000m, "Lost", 1, 16, 9, "Documents requested 09-09.", "Client went silent after the document request."),
            D(carlos, "R. Chen", "Auto Loan", 25_000m, "Lost", 7, 15, 14, "Compared offers.", "Chose dealer financing."),

            D(sofia, "A. Ramos", "Refinance", 62_000m, "Funded", 3, 18, 18, "First funded loan! Walked the client through closing documents."),
            D(sofia, "Lopez Family", "Mortgage", 280_000m, "Application", 11, 16, 17, "Application complete; ordering appraisal."),
            D(sofia, "Harbor Café", "SBA Loan", 90_000m, "Documents Pending", 8, 15, 17, "Followed up twice on missing documents."),
            D(sofia, "D. Park", "Personal Loan", 10_000m, "Lead", 17, 17, 17, "Inbound lead; intro call done."),

            D(miguel, "Whitaker Family", "Mortgage", 140_000m, "Funded", 1, 15, 15, "Funded; client very satisfied."),
            D(miguel, "Coastal Freight Co.", "SBA Loan", 350_000m, "Underwriting", 1, 11, 11, "Waiting on equipment appraisal."),
            D(miguel, "S. Ivanova", "Refinance", 190_000m, "Approved", 2, 12, 12, "Approved; closing date not yet scheduled."),
            D(miguel, "E. Walsh", "Mortgage", 260_000m, "Application", 8, 10, 10, "Application received; credit pull pending."),
            D(miguel, "P. Grant", "HELOC", 75_000m, "Lost", 3, 16, 16, "Client reconsidering.", "Client postponed the renovation."),

            D(laura, "N. Brooks", "Auto Loan", 33_000m, "Funded", 8, 15, 15, "Funded in 7 days."),
            D(laura, "M. Diaz", "HELOC", 55_000m, "Funded", 2, 17, 17, "Funded; client asked about a future refinance."),
            D(laura, "Oakridge Pharmacy", "SBA Loan", 400_000m, "Lead", 16, 16, 18, "Referral from an existing client. Intro call went well."),
            D(laura, "J. Harper", "Refinance", 45_000m, "Underwriting", 4, 14, 18, "All documents in; underwriting on track."),
            D(laura, "C. Evans", "Auto Loan", 22_000m, "Application", 15, 16, 18, "Application submitted."));

        db.CoachingReports.Add(new CoachingReport
        {
            Rep = miguel, Week = current, Status = ReportStatus.NotGenerated,
            ManagerNote = "Out 2 days this week for compliance training.",
        });

        await db.SaveChangesAsync(ct);

        AppUser User(string username, string displayName, UserRole role, Rep? rep)
        {
            var user = new AppUser { Username = username, DisplayName = displayName, Role = role, Rep = rep };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        }
    }

    /// <summary>
    /// Pre-approves one rep's report with the demo feedback, so a reviewer who signs in as that rep first
    /// sees a finished result. Idempotent: does nothing if the report already exists.
    /// </summary>
    public static Task ApproveDemoReportAsync(AppDbContext db, string repName, string contentJson,
        string modelName, DateTime approvedAtUtc, CancellationToken ct = default) =>
        ApproveDemoReportAsync(db, repName, weekStart: null, contentJson, modelName, approvedAtUtc, ct);

    /// <summary>Same as above for a specific week (by start date); null means the current week.</summary>
    public static async Task ApproveDemoReportAsync(AppDbContext db, string repName, DateOnly? weekStart, string contentJson,
        string modelName, DateTime approvedAtUtc, CancellationToken ct = default)
    {
        var rep = await db.Reps.SingleOrDefaultAsync(r => r.Name == repName, ct);
        var week = weekStart is DateOnly start
            ? await db.Weeks.SingleOrDefaultAsync(w => w.StartDate == start, ct)
            : await db.Weeks.SingleOrDefaultAsync(w => w.IsCurrent, ct);
        var manager = await db.Users.SingleOrDefaultAsync(u => u.Role == UserRole.Manager, ct);
        if (rep is null || week is null || manager is null) return;
        if (await db.CoachingReports.AnyAsync(r => r.RepId == rep.Id && r.WeekId == week.Id, ct)) return;

        db.CoachingReports.Add(new CoachingReport
        {
            RepId = rep.Id, WeekId = week.Id, Status = ReportStatus.Approved, Source = ReportSource.Demo,
            ContentJson = contentJson, ModelName = modelName, IsEdited = false,
            GeneratedAtUtc = approvedAtUtc.AddMinutes(-20), ApprovedAtUtc = approvedAtUtc, ApprovedByUserId = manager.Id,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fictional history so the archive has something to show: Laura's approved report for the previous
    /// week (2026-09-07 → 09-11), consistent with her previous-week metrics in the seed.
    /// </summary>
    public static Task SeedHistoryAsync(AppDbContext db, CancellationToken ct = default) =>
        ApproveDemoReportAsync(db, "Laura Bennett", new DateOnly(2026, 9, 7), LauraPreviousWeekJson,
            "pre-generated demo", new DateTime(2026, 9, 11, 22, 0, 0, DateTimeKind.Utc), ct);

    private const string LauraPreviousWeekJson = """
        {
          "summary": "A consistent week: two loans funded and the fastest replies on the team. The next step toward your goal is getting a larger file into the pipeline.",
          "strengths": [
            { "title": "Two loans funded", "detail": "Both files moved cleanly from application to funding.", "evidence": "2 loans funded for $79,000 (52.7% of your $150,000 quota)." },
            { "title": "Best email engagement on the team", "detail": "Your follow-ups get answered.", "evidence": "20 replies from 58 emails (34%); average lead response 2.2h." }
          ],
          "improvements": [
            { "title": "No large files in the pipeline yet", "detail": "Your funded volume is limited by loan size rather than by activity or conversion.", "evidence": "3 applications submitted, 2 funded, average loan size under $40,000." }
          ],
          "actions": [
            { "action": "Ask each funded client for one referral to a business owner", "why": "Commercial referrals are the fastest route to larger loans.", "successSignal": "At least one SBA or commercial lead added next week." },
            { "action": "Block one hour on Wednesday to prospect local businesses", "why": "Larger loans need dedicated prospecting time, not just inbound leads.", "successSignal": "Two new business conversations logged." }
          ],
          "dataGaps": []
        }
        """;

    private static Rep NewRep(string name, string email, string title, int tenureMonths, decimal quota, string goal, FeedbackStyle style) =>
        new() { Name = name, Email = email, Title = title, TenureMonths = tenureMonths, WeeklyQuota = quota, PersonalGoal = goal, FeedbackStyle = style };

    private static WeeklyMetrics M(Rep rep, Week week, int calls, int connected, int emails, int replied,
        int leads, int applications, int won, decimal volume, int lost, double responseHours) =>
        new()
        {
            Rep = rep, Week = week, CallsMade = calls, CallsConnected = connected, EmailsSent = emails,
            EmailsReplied = replied, NewLeads = leads, ApplicationsSubmitted = applications, DealsWon = won,
            WonVolume = volume, DealsLost = lost, AvgLeadResponseHours = responseHours,
        };

    private static Deal D(Rep rep, string client, string product, decimal amount, string stage,
        int createdDay, int stageChangedDay, int lastContactDay, string notes, string? lostReason = null) =>
        new()
        {
            Rep = rep, ClientName = client, Product = product, Amount = amount, Stage = stage,
            CreatedOn = new DateOnly(2026, 9, createdDay),
            StageChangedOn = new DateOnly(2026, 9, stageChangedDay),
            LastContactOn = new DateOnly(2026, 9, lastContactDay),
            Notes = notes, LostReason = lostReason,
        };
}
