using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingServiceTests
{
    private static (int RepA, int RepB, int WeekId) Seed(TestDb t)
    {
        using var db = t.CreateContext();
        var week = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
        var a = TestData.AddRep(db, "Ana Torres");
        var b = TestData.AddRep(db, "Carlos Mendoza");
        TestData.AddMetrics(db, a, week);
        TestData.AddMetrics(db, b, week);
        db.SaveChanges();
        return (a.Id, b.Id, week.Id);
    }

    private static CoachingService Service(TestDb t, IFeedbackGenerator generator) => new(
        t.Factory,
        new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions())),
        generator,
        TimeProvider.System,
        NullLogger<CoachingService>.Instance);

    private static Func<string> Valid => () => TestContent.ValidJson;

    [Fact]
    public async Task Generate_saves_a_draft_with_source_and_model()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);

        var report = await Service(t, new FakeFeedbackGenerator(Valid)).GenerateAsync(repA, weekId);

        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(ReportSource.Demo, report.Source);
        Assert.Equal("fake-model", report.ModelName);
        Assert.False(report.IsEdited);
        Assert.NotNull(report.GeneratedAtUtc);
        Assert.Equal("Solid week.", CoachingService.ReadContent(report)!.Summary);
    }

    [Fact]
    public async Task Generate_retries_once_on_invalid_content()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var generator = new FakeFeedbackGenerator(() => "not json", Valid);

        var report = await Service(t, generator).GenerateAsync(repA, weekId);

        Assert.Equal(2, generator.Calls);
        Assert.Equal(ReportStatus.Draft, report.Status);
    }

    [Fact]
    public async Task Generate_gives_up_after_two_invalid_responses_and_saves_nothing()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var generator = new FakeFeedbackGenerator(() => "not json", () => "{}");

        await Assert.ThrowsAsync<InvalidCoachingContentException>(() => Service(t, generator).GenerateAsync(repA, weekId));

        await using var db = t.CreateContext();
        var report = await db.CoachingReports.SingleAsync(r => r.RepId == repA);
        Assert.Equal(ReportStatus.NotGenerated, report.Status);
        Assert.Null(report.ContentJson);
    }

    [Fact]
    public async Task Generate_keeps_the_existing_draft_when_the_ai_is_unavailable()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var first = await Service(t, new FakeFeedbackGenerator(Valid)).GenerateAsync(repA, weekId);
        var failing = new FakeFeedbackGenerator(() => throw new AiUnavailableException(AiFailureKind.RateLimited, "429"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Service(t, failing).GenerateAsync(repA, weekId));

        Assert.Equal(AiFailureKind.RateLimited, ex.Kind);
        await using var db = t.CreateContext();
        var report = await db.CoachingReports.SingleAsync(r => r.RepId == repA);
        Assert.Equal(ReportStatus.Draft, report.Status);
        Assert.Equal(first.ContentJson, report.ContentJson);
    }

    [Fact]
    public async Task Drafts_are_not_visible_to_the_rep()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        await service.GenerateAsync(repA, weekId);

        Assert.Null(await service.GetApprovedForRepAsync(repA, weekId));
    }

    [Fact]
    public async Task Approved_report_is_visible_only_to_its_own_rep()
    {
        using var t = new TestDb();
        var (repA, repB, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid, Valid));
        var reportA = await service.GenerateAsync(repA, weekId);
        await service.GenerateAsync(repB, weekId);

        await service.ApproveAsync(reportA.Id, approvedByUserId: 1);

        var visibleToA = await service.GetApprovedForRepAsync(repA, weekId);
        Assert.NotNull(visibleToA);
        Assert.Equal(repA, visibleToA.RepId);
        Assert.Equal(1, visibleToA.ApprovedByUserId);
        Assert.Null(await service.GetApprovedForRepAsync(repB, weekId));
    }

    [Fact]
    public async Task Approved_report_cannot_be_edited_regenerated_or_noted()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid, Valid));
        var report = await service.GenerateAsync(repA, weekId);
        await service.ApproveAsync(report.Id, approvedByUserId: 1);
        var content = CoachingService.ReadContent(report)!;
        content.Summary = "Changed after approval";

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveEditsAsync(report.Id, content));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(repA, weekId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveManagerNoteAsync(repA, weekId, "late note"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(report.Id, approvedByUserId: 1));

        var stored = await service.GetApprovedForRepAsync(repA, weekId);
        Assert.Equal("Solid week.", CoachingService.ReadContent(stored!)!.Summary);
    }

    [Fact]
    public async Task Reopen_turns_an_approved_report_into_a_protected_draft_the_rep_no_longer_sees()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        var report = await service.GenerateAsync(repA, weekId);
        await service.ApproveAsync(report.Id, approvedByUserId: 1);

        await service.ReopenAsync(report.Id);

        var reopened = await service.GetOrCreateReportAsync(repA, weekId);
        Assert.Equal(ReportStatus.Draft, reopened.Status);
        Assert.True(reopened.IsEdited);
        Assert.Null(reopened.ApprovedAtUtc);
        Assert.Null(reopened.ApprovedByUserId);
        Assert.Equal("Solid week.", CoachingService.ReadContent(reopened)!.Summary);
        Assert.Null(await service.GetApprovedForRepAsync(repA, weekId));

        // A team-wide run must not overwrite text the rep already saw.
        Assert.False(await service.GenerateIfEligibleAsync(repA, weekId));

        // Edit and approve again: visible once more.
        var content = CoachingService.ReadContent(reopened)!;
        content.Summary = "Revised after our 1:1.";
        Assert.True(await service.SaveEditsAsync(reopened.Id, content));
        await service.ApproveAsync(reopened.Id, approvedByUserId: 2);
        var visible = await service.GetApprovedForRepAsync(repA, weekId);
        Assert.Equal("Revised after our 1:1.", CoachingService.ReadContent(visible!)!.Summary);
        Assert.Equal(2, visible!.ApprovedByUserId);
    }

    [Fact]
    public async Task Reopen_refuses_a_report_that_is_not_approved()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        var draft = await service.GenerateAsync(repA, weekId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReopenAsync(draft.Id));
        Assert.Equal(ReportStatus.Draft, (await service.GetOrCreateReportAsync(repA, weekId)).Status);
    }

    [Fact]
    public async Task SaveEdits_marks_the_report_as_edited_only_when_content_changes()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        var report = await service.GenerateAsync(repA, weekId);

        var savedUnchanged = await service.SaveEditsAsync(report.Id, CoachingService.ReadContent(report)!);
        Assert.False(savedUnchanged);
        Assert.False((await service.GetOrCreateReportAsync(repA, weekId)).IsEdited);

        var edited = CoachingService.ReadContent(report)!;
        edited.Summary = "Edited by the manager.";
        var savedChanged = await service.SaveEditsAsync(report.Id, edited);
        Assert.True(savedChanged);

        var stored = await service.GetOrCreateReportAsync(repA, weekId);
        Assert.True(stored.IsEdited);
        Assert.Equal("Edited by the manager.", CoachingService.ReadContent(stored)!.Summary);
    }

    [Fact]
    public async Task SaveEdits_rejects_content_without_evidence()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator(Valid));
        var report = await service.GenerateAsync(repA, weekId);
        var edited = CoachingService.ReadContent(report)!;
        edited.Strengths[0].Evidence = " ";

        await Assert.ThrowsAsync<InvalidCoachingContentException>(() => service.SaveEditsAsync(report.Id, edited));
    }

    [Fact]
    public async Task GenerateIfEligible_skips_reports_approved_or_edited_since_the_dashboard_loaded()
    {
        using var t = new TestDb();
        var (repA, repB, weekId) = Seed(t);
        var setup = Service(t, new FakeFeedbackGenerator(Valid, Valid));
        var approved = await setup.GenerateAsync(repA, weekId);
        await setup.ApproveAsync(approved.Id, approvedByUserId: 1);
        var edited = await setup.GenerateAsync(repB, weekId);
        var content = CoachingService.ReadContent(edited)!;
        content.Summary = "Hand-edited in another tab.";
        await setup.SaveEditsAsync(edited.Id, content);
        var generator = new FakeFeedbackGenerator();

        var generatedA = await Service(t, generator).GenerateIfEligibleAsync(repA, weekId);
        var generatedB = await Service(t, generator).GenerateIfEligibleAsync(repB, weekId);

        Assert.False(generatedA);
        Assert.False(generatedB);
        Assert.Equal(0, generator.Calls);
        var stored = await setup.GetOrCreateReportAsync(repB, weekId);
        Assert.Equal("Hand-edited in another tab.", CoachingService.ReadContent(stored)!.Summary);
    }

    [Fact]
    public async Task GenerateIfEligible_generates_new_and_unedited_drafts()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var generator = new FakeFeedbackGenerator(Valid);

        var generated = await Service(t, generator).GenerateIfEligibleAsync(repA, weekId);

        Assert.True(generated);
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public async Task History_lists_every_week_for_the_manager_but_only_approved_weeks_for_the_rep()
    {
        using var t = new TestDb();
        int repA, oldWeekId, currentWeekId;
        using (var db = t.CreateContext())
        {
            var oldWeek = TestData.AddWeek(db, new DateOnly(2026, 9, 7), isCurrent: false);
            var older = TestData.AddWeek(db, new DateOnly(2026, 8, 31), isCurrent: false);
            var current = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var a = TestData.AddRep(db, "Laura Bennett");
            TestData.AddMetrics(db, a, oldWeek);
            TestData.AddMetrics(db, a, older);
            TestData.AddMetrics(db, a, current);
            db.SaveChanges();
            repA = a.Id; oldWeekId = oldWeek.Id; currentWeekId = current.Id;
        }
        var service = Service(t, new FakeFeedbackGenerator(Valid, Valid));
        var oldReport = await service.GenerateAsync(repA, oldWeekId);
        await service.ApproveAsync(oldReport.Id, approvedByUserId: 1);
        await service.GenerateAsync(repA, currentWeekId); // stays a draft

        var managerView = await service.GetHistoryAsync(repA);
        var repView = await service.GetApprovedHistoryAsync(repA);

        Assert.Equal([currentWeekId, oldWeekId], managerView.Select(h => h.Week.Id));
        Assert.Equal([ReportStatus.Draft, ReportStatus.Approved], managerView.Select(h => h.Report.Status));
        var only = Assert.Single(repView);
        Assert.Equal(oldWeekId, only.Week.Id);
        Assert.Equal("Solid week.", CoachingService.ReadContent(only.Report)!.Summary);
    }

    [Fact]
    public async Task GenerateTeam_continues_past_a_failing_rep_and_reports_each_outcome()
    {
        using var t = new TestDb();
        var (repA, repB, weekId) = Seed(t);
        // Rep A fails validation twice (retry exhausted); rep B succeeds.
        var generator = new FakeFeedbackGenerator(() => "not json", () => "{}", Valid);
        var progress = new List<string>();

        var result = await Service(t, generator).GenerateTeamAsync(weekId, name => progress.Add(name));

        Assert.Equal(["Ana Torres", "Carlos Mendoza"], progress);
        Assert.Equal(["Carlos Mendoza"], result.Generated);
        var failure = Assert.Single(result.Failed);
        Assert.Equal("Ana Torres", failure.RepName);
        Assert.Contains("incomplete", failure.Message);
        Assert.Empty(result.Skipped);
        Assert.Equal(3, generator.Calls);
    }

    [Fact]
    public async Task GenerateTeam_skips_approved_and_edited_reports_and_stops_when_the_ai_is_unavailable()
    {
        using var t = new TestDb();
        var (repA, repB, weekId) = Seed(t);
        var setup = Service(t, new FakeFeedbackGenerator(Valid));
        var approved = await setup.GenerateAsync(repA, weekId);
        await setup.ApproveAsync(approved.Id, approvedByUserId: 1);
        var generator = new FakeFeedbackGenerator(() => throw new AiUnavailableException(AiFailureKind.RateLimited, "429"));

        var result = await Service(t, generator).GenerateTeamAsync(weekId, _ => { });

        Assert.Equal(["Ana Torres"], result.Skipped);
        Assert.Empty(result.Generated);
        var failure = Assert.Single(result.Failed);
        Assert.Equal("Carlos Mendoza", failure.RepName);
        Assert.Contains("usage limit", failure.Message);
        Assert.True(result.StoppedEarly);
    }

    [Fact]
    public async Task Manager_note_is_saved_trimmed_and_limited_in_length()
    {
        using var t = new TestDb();
        var (repA, _, weekId) = Seed(t);
        var service = Service(t, new FakeFeedbackGenerator());

        await service.SaveManagerNoteAsync(repA, weekId, "  Out 2 days.  ");
        Assert.Equal("Out 2 days.", (await service.GetOrCreateReportAsync(repA, weekId)).ManagerNote);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveManagerNoteAsync(repA, weekId, new string('x', CoachingService.ManagerNoteMaxLength + 1)));
    }
}
