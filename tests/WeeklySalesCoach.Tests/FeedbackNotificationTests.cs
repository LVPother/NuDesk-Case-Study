using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Services.Notifications;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class FeedbackNotificationTests
{
    private sealed class FakeEmailSender(bool configured, EmailSendResult result) : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public string ProviderName => "fake";
        public bool IsConfigured => configured;

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.FromResult(result);
        }
    }

    private static async Task<(TestDb Db, int ApprovedReportId, int DraftReportId)> SeedWithApprovedReportAsync()
    {
        var t = new TestDb();
        int repA, repB, weekId, managerId;
        using (var db = t.CreateContext())
        {
            var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var a = TestData.AddRep(db, "Laura Bennett");
            a.Email = "laura.bennett@example.com";
            var b = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, a, week);
            TestData.AddMetrics(db, b, week);
            var manager = new AppUser { Username = "roberto", DisplayName = "Roberto Díaz", Role = UserRole.Manager, PasswordHash = "x" };
            db.Users.Add(manager);
            db.SaveChanges();
            repA = a.Id; repB = b.Id; weekId = week.Id; managerId = manager.Id;
        }

        var coaching = new CoachingService(t.Factory,
            new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions())),
            new FakeFeedbackGenerator(() => TestContent.ValidJson, () => TestContent.ValidJson),
            TimeProvider.System, NullLogger<CoachingService>.Instance);
        var approved = await coaching.GenerateAsync(repA, weekId);
        await coaching.ApproveAsync(approved.Id, managerId);
        var draft = await coaching.GenerateAsync(repB, weekId);
        return (t, approved.Id, draft.Id);
    }

    private static FeedbackNotificationService Service(TestDb t, IEmailSender sender) =>
        new(t.Factory, sender, TimeProvider.System, NullLogger<FeedbackNotificationService>.Instance);

    [Fact]
    public void Composer_turns_approved_content_into_a_readable_email()
    {
        var content = CoachingContentParser.Parse(TestContent.ValidJson);

        var email = FeedbackEmailComposer.Compose("laura.bennett@example.com", "Laura Bennett", "Roberto Díaz",
            new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18), content);

        Assert.Equal("laura.bennett@example.com", email.To);
        Assert.Contains("Sep 14", email.Subject);
        Assert.Contains("Hi Laura", email.Body);
        Assert.Contains("Solid week.", email.Body);
        Assert.Contains("Closed Delgado", email.Body);
        Assert.Contains("Delgado Family mortgage, $95,000, funded 09-17.", email.Body);
        Assert.Contains("Call every Documents Pending client by Tuesday", email.Body);
        Assert.Contains("Roberto Díaz", email.Body);
    }

    [Fact]
    public async Task Queues_the_email_when_no_provider_is_configured()
    {
        var (t, approvedId, _) = await SeedWithApprovedReportAsync();
        using var owned = t;
        var sender = new FakeEmailSender(configured: false, new EmailSendResult(false, "No email provider configured."));

        var email = await Service(t, sender).QueueApprovedReportAsync(approvedId);

        Assert.Equal(EmailStatus.Queued, email.Status);
        Assert.Equal("laura.bennett@example.com", email.ToAddress);
        Assert.Contains("Solid week.", email.Body);
        Assert.Null(email.SentAtUtc);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Sends_and_marks_as_sent_when_a_provider_is_configured()
    {
        var (t, approvedId, _) = await SeedWithApprovedReportAsync();
        using var owned = t;
        var sender = new FakeEmailSender(configured: true, new EmailSendResult(true, null));

        var email = await Service(t, sender).QueueApprovedReportAsync(approvedId);

        Assert.Equal(EmailStatus.Sent, email.Status);
        Assert.Equal("fake", email.Provider);
        Assert.NotNull(email.SentAtUtc);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task Records_the_error_when_the_provider_fails()
    {
        var (t, approvedId, _) = await SeedWithApprovedReportAsync();
        using var owned = t;
        var sender = new FakeEmailSender(configured: true, new EmailSendResult(false, "SMTP 550"));

        var email = await Service(t, sender).QueueApprovedReportAsync(approvedId);

        Assert.Equal(EmailStatus.Failed, email.Status);
        Assert.Equal("SMTP 550", email.Error);
    }

    [Fact]
    public async Task Queues_only_once_per_report()
    {
        var (t, approvedId, _) = await SeedWithApprovedReportAsync();
        using var owned = t;
        var service = Service(t, new FakeEmailSender(configured: false, new EmailSendResult(false, null)));

        var first = await service.QueueApprovedReportAsync(approvedId);
        var second = await service.QueueApprovedReportAsync(approvedId);

        Assert.Equal(first.Id, second.Id);
        await using var db = t.CreateContext();
        Assert.Equal(1, await db.FeedbackEmails.CountAsync());
        Assert.Equal(first.Id, (await service.GetForReportAsync(approvedId))!.Id);
    }

    [Fact]
    public async Task A_revised_and_reapproved_report_gets_a_second_updated_email()
    {
        var (t, approvedId, _) = await SeedWithApprovedReportAsync();
        using var owned = t;
        var sender = new FakeEmailSender(configured: false, new EmailSendResult(false, "No email provider configured."));
        var service = Service(t, sender);
        var first = await service.QueueApprovedReportAsync(approvedId);

        var coaching = new CoachingService(t.Factory,
            new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions())),
            new FakeFeedbackGenerator(() => TestContent.ValidJson),
            TimeProvider.System, NullLogger<CoachingService>.Instance);
        await coaching.ReopenAsync(approvedId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueApprovedReportAsync(approvedId));
        await Task.Delay(20); // the new approval must be later than the first email
        await coaching.ApproveAsync(approvedId, approvedByUserId: 1);

        var second = await service.QueueApprovedReportAsync(approvedId);
        var again = await service.QueueApprovedReportAsync(approvedId);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, again.Id);
        Assert.StartsWith("Updated:", second.Subject);
        Assert.Contains("replaces the one you received earlier", second.Body);
        Assert.Equal(second.Id, (await service.GetForReportAsync(approvedId))!.Id);
    }

    [Fact]
    public async Task Refuses_to_email_a_report_that_is_not_approved()
    {
        var (t, _, draftId) = await SeedWithApprovedReportAsync();
        using var owned = t;
        var service = Service(t, new FakeEmailSender(configured: false, new EmailSendResult(false, null)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueueApprovedReportAsync(draftId));
        Assert.Null(await service.GetForReportAsync(draftId));
    }
}
