using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class CoachingPromptTests
{
    [Fact]
    public void System_instructions_contain_the_key_guardrails()
    {
        var s = CoachingPrompt.SystemInstructions;

        Assert.Contains("evidence", s);
        Assert.Contains("HR actions", s);
        Assert.Contains("by name", s);
        Assert.Contains("dataGaps", s);
        Assert.Contains("manager note", s);
    }

    [Fact]
    public void User_message_contains_this_reps_data_and_manager_note()
    {
        var ctx = TestContent.SampleContext() with { ManagerNote = "Out 2 days for training." };

        var message = CoachingPrompt.BuildUserMessage(ctx);

        Assert.Contains("Ana Torres", message);
        Assert.Contains("Hartwell Family", message);
        Assert.Contains("Out 2 days for training.", message);
        Assert.Contains("\"teamAverage\"", message);
    }

    [Fact]
    public async Task User_message_never_contains_other_reps_names()
    {
        using var t = new TestDb();
        int anaId, weekId;
        using (var db = t.CreateContext())
        {
            var cur = TestData.AddWeek(db, new DateOnly(2026, 9, 14), isCurrent: true);
            var ana = TestData.AddRep(db, "Ana Torres");
            var carlos = TestData.AddRep(db, "Carlos Mendoza");
            TestData.AddMetrics(db, ana, cur);
            TestData.AddMetrics(db, carlos, cur);
            TestData.AddDeal(db, carlos, "Carlos Client", "Lead", 10_000m, new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 15));
            db.SaveChanges();
            anaId = ana.Id; weekId = cur.Id;
        }
        var builder = new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions()));

        var message = CoachingPrompt.BuildUserMessage(await builder.BuildAsync(anaId, weekId));

        Assert.Contains("Ana Torres", message);
        Assert.DoesNotContain("Carlos", message);
    }
}
