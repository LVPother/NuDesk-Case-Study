using System.Text;
using WeeklySalesCoach.Auth;

namespace WeeklySalesCoach.Services.Export;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        // Manager: every report (all reps, all weeks, drafts included).
        app.MapGet("/export/feedback.csv", async (FeedbackCsvService csv, CancellationToken ct) =>
            Csv(await csv.BuildForManagerAsync(ct), "weekly-sales-coach-feedback.csv"))
            .RequireAuthorization(p => p.RequireRole("Manager"));

        // Rep: only their own approved reports; the rep id comes from the auth cookie.
        app.MapGet("/export/my-feedback.csv", async (HttpContext http, FeedbackCsvService csv, CancellationToken ct) =>
            http.User.GetRepId() is int repId
                ? Csv(await csv.BuildForRepAsync(repId, ct), "my-feedback.csv")
                : Results.Forbid())
            .RequireAuthorization(p => p.RequireRole("Rep"));
    }

    private static IResult Csv(string content, string fileName) =>
        Results.File(Encoding.UTF8.GetBytes(content), "text/csv; charset=utf-8", fileName);
}
