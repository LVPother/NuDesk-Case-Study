using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeeklySalesCoach.Data;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>
/// Dev-only: runs the real AI for every rep and saves the results as the demo-mode feedback file.
/// Progress is kept in "&lt;output&gt;.partial" after each rep, so a rerun after a failure (e.g. free-tier
/// rate limits) only generates the reps that are still missing.
/// </summary>
public class DemoFeedbackExporter(
    IDbContextFactory<AppDbContext> dbFactory,
    CoachingContextBuilder contextBuilder,
    ILogger<DemoFeedbackExporter> logger)
{
    private const int MaxAttemptsPerRep = 4;
    private const int RateLimitDelayMultiplier = 6;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<int> ExportAsync(IFeedbackGenerator generator, string outputPath,
        TimeSpan? retryDelay = null, CancellationToken ct = default)
    {
        var delay = retryDelay ?? TimeSpan.FromSeconds(10);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var week = await db.Weeks.AsNoTracking().SingleAsync(w => w.IsCurrent, ct);
        var reps = await db.Reps.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var partialPath = outputPath + ".partial";
        var result = await LoadPartialAsync(partialPath, ct);

        foreach (var rep in reps.Where(r => !result.ContainsKey(r.Name)))
        {
            var context = await contextBuilder.BuildAsync(rep.Id, week.Id, ct);
            result[rep.Name] = await GenerateWithRetryAsync(generator, context, delay, ct);
            await File.WriteAllTextAsync(partialPath, JsonSerializer.Serialize(result, Json), ct);
            logger.LogInformation("Generated demo feedback for {Rep}", rep.Name);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(result, Json), ct);
        File.Delete(partialPath);
        return result.Count;
    }

    private static async Task<SortedDictionary<string, CoachingContent>> LoadPartialAsync(string path, CancellationToken ct)
    {
        var result = new SortedDictionary<string, CoachingContent>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;

        var saved = JsonSerializer.Deserialize<Dictionary<string, CoachingContent>>(await File.ReadAllTextAsync(path, ct), Json);
        foreach (var (name, content) in saved ?? [])
            result[name] = content;
        return result;
    }

    /// <summary>Retries invalid output and transient outages (busy, rate-limited, timeout), waiting between attempts.</summary>
    private async Task<CoachingContent> GenerateWithRetryAsync(
        IFeedbackGenerator generator, CoachingContext context, TimeSpan delay, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return CoachingContentParser.Parse(await generator.GenerateAsync(context, ct));
            }
            catch (Exception ex) when (attempt < MaxAttemptsPerRep && IsRetryable(ex))
            {
                logger.LogWarning("Attempt {Attempt} for {Rep} failed ({Reason}); retrying.", attempt, context.Rep.Name, ex.Message);
                // Free-tier rate limits are per minute, so back off much longer for them.
                var wait = ex is AiUnavailableException { Kind: AiFailureKind.RateLimited } ? delay * RateLimitDelayMultiplier : delay;
                await Task.Delay(wait * attempt, ct);
            }
        }
    }

    public static bool IsRetryable(Exception ex) => ex is InvalidCoachingContentException
        || ex is AiUnavailableException { Kind: not (AiFailureKind.NotConfigured or AiFailureKind.InvalidRequest) };
}
