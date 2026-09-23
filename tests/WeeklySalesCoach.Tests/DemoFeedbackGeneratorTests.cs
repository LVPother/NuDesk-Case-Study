using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class DemoFeedbackGeneratorTests
{
    [Fact]
    public async Task Returns_pre_generated_json_for_a_known_rep()
    {
        var path = Path.Combine(Path.GetTempPath(), $"demo-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"Ana Torres\": " + TestContent.ValidJson + " }");
        try
        {
            var generator = new DemoFeedbackGenerator(path);

            var raw = await generator.GenerateAsync(TestContent.SampleContext("Ana Torres"));

            Assert.False(generator.IsLive);
            Assert.Equal("Solid week.", CoachingContentParser.Parse(raw).Summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Unknown_rep_raises_unavailable()
    {
        var generator = new DemoFeedbackGenerator(Path.Combine(Path.GetTempPath(), "does-not-exist.json"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => generator.GenerateAsync(TestContent.SampleContext("Nobody")));

        Assert.Equal(AiFailureKind.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Committed_demo_feedback_covers_every_seeded_rep()
    {
        using var t = new TestDb();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        }
        await using var check = t.CreateContext();
        var names = await check.Reps.Select(r => r.Name).ToListAsync();
        var generator = new DemoFeedbackGenerator(Path.Combine(AppContext.BaseDirectory, "demo-feedback.json"));

        foreach (var name in names)
        {
            var raw = await generator.GenerateAsync(TestContent.SampleContext(name));
            var content = CoachingContentParser.Parse(raw);
            Assert.False(string.IsNullOrWhiteSpace(content.Summary), name);
        }
    }
}
