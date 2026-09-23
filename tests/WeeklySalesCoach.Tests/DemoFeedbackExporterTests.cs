using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class DemoFeedbackExporterTests
{
    [Fact]
    public async Task Exports_every_rep_in_a_format_the_demo_generator_reads()
    {
        using var t = new TestDb();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        }
        var builder = new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions()));
        var generator = new FakeFeedbackGenerator(Enumerable.Repeat<Func<string>>(() => TestContent.ValidJson, 5).ToArray());
        var exporter = new DemoFeedbackExporter(t.Factory, builder, NullLogger<DemoFeedbackExporter>.Instance);
        var path = Path.Combine(Path.GetTempPath(), $"demo-{Guid.NewGuid():N}.json");
        try
        {
            var count = await exporter.ExportAsync(generator, path);

            Assert.Equal(5, count);
            var raw = await new DemoFeedbackGenerator(path).GenerateAsync(TestContent.SampleContext("Sofía Herrera"));
            Assert.Equal("Solid week.", CoachingContentParser.Parse(raw).Summary);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Keeps_progress_after_a_failure_and_only_generates_missing_reps_on_rerun()
    {
        using var t = new TestDb();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        }
        var builder = new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions()));
        var exporter = new DemoFeedbackExporter(t.Factory, builder, NullLogger<DemoFeedbackExporter>.Instance);
        Func<string> ok = () => TestContent.ValidJson;
        Func<string> fatal = () => throw new AiUnavailableException(AiFailureKind.NotConfigured, "no key");
        var path = Path.Combine(Path.GetTempPath(), $"demo-{Guid.NewGuid():N}.json");
        try
        {
            // First run: Ana and Carlos succeed, then a non-retryable failure stops the run.
            await Assert.ThrowsAsync<AiUnavailableException>(() =>
                exporter.ExportAsync(new FakeFeedbackGenerator(ok, ok, fatal), path, retryDelay: TimeSpan.Zero));
            Assert.False(File.Exists(path));

            // Second run: only the 3 missing reps are generated, then the final file is written.
            var rerun = new FakeFeedbackGenerator(ok, ok, ok);
            var count = await exporter.ExportAsync(rerun, path, retryDelay: TimeSpan.Zero);

            Assert.Equal(5, count);
            Assert.Equal(3, rerun.Calls);
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".partial"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".partial");
        }
    }

    [Fact]
    public async Task Retries_transient_ai_outages_before_giving_up()
    {
        using var t = new TestDb();
        await using (var db = t.CreateContext())
        {
            await SeedData.SeedAsync(db, new PasswordHasher<AppUser>());
        }
        var builder = new CoachingContextBuilder(t.Factory, Options.Create(new DomainOptions()));
        Func<string> busy = () => throw new AiUnavailableException(AiFailureKind.Unavailable, "503 high demand");
        Func<string> ok = () => TestContent.ValidJson;
        var generator = new FakeFeedbackGenerator(busy, busy, ok, ok, ok, ok, ok);
        var exporter = new DemoFeedbackExporter(t.Factory, builder, NullLogger<DemoFeedbackExporter>.Instance);
        var path = Path.Combine(Path.GetTempPath(), $"demo-{Guid.NewGuid():N}.json");
        try
        {
            var count = await exporter.ExportAsync(generator, path, retryDelay: TimeSpan.Zero);

            Assert.Equal(5, count);
            Assert.Equal(7, generator.Calls);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
