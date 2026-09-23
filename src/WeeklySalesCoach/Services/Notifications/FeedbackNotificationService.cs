using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.Notifications;

/// <summary>
/// Composes the email for an approved report and records it in the outbox. Sends it right away when an
/// <see cref="IEmailSender"/> is configured; otherwise it stays Queued so a provider can pick it up later.
/// </summary>
public class FeedbackNotificationService(
    IDbContextFactory<AppDbContext> dbFactory,
    IEmailSender sender,
    TimeProvider time,
    ILogger<FeedbackNotificationService> logger)
{
    public async Task<FeedbackEmail> QueueApprovedReportAsync(int reportId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var report = await db.CoachingReports.Include(r => r.Rep).Include(r => r.Week)
            .SingleAsync(r => r.Id == reportId, ct);
        if (report.Status != ReportStatus.Approved || report.ContentJson is null)
            throw new InvalidOperationException("Only approved feedback can be emailed.");

        // One email per approval: the latest one already covers this approval, an older one means the report was revised.
        var latest = await db.FeedbackEmails.Where(e => e.ReportId == reportId)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        if (latest is not null && report.ApprovedAtUtc is DateTime approvedAt && latest.CreatedAtUtc >= approvedAt) return latest;
        var isUpdate = latest is not null;

        var manager = report.ApprovedByUserId is int approverId
            ? await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == approverId, ct)
            : null;
        var message = FeedbackEmailComposer.Compose(
            report.Rep!.Email, report.Rep.Name, manager?.DisplayName ?? "Your manager",
            report.Week!.StartDate, report.Week.EndDate, CoachingContentParser.FromStored(report.ContentJson), isUpdate);

        var email = new FeedbackEmail
        {
            ReportId = reportId,
            ToAddress = message.To,
            Subject = message.Subject,
            Body = message.Body,
            Provider = sender.ProviderName,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime,
        };

        if (sender.IsConfigured)
        {
            var result = await sender.SendAsync(message, ct);
            email.Status = result.Sent ? EmailStatus.Sent : EmailStatus.Failed;
            email.SentAtUtc = result.Sent ? time.GetUtcNow().UtcDateTime : null;
            email.Error = result.Sent ? null : result.Error;
            if (!result.Sent) logger.LogWarning("Email for report {ReportId} failed: {Error}", reportId, result.Error);
        }
        else
        {
            logger.LogInformation("Email for report {ReportId} queued (no email provider configured).", reportId);
        }

        db.FeedbackEmails.Add(email);
        await db.SaveChangesAsync(ct);
        return email;
    }

    /// <summary>The most recent email for a report (a revised report has one per approval).</summary>
    public async Task<FeedbackEmail?> GetForReportAsync(int reportId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.FeedbackEmails.AsNoTracking().Where(e => e.ReportId == reportId)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
    }
}
