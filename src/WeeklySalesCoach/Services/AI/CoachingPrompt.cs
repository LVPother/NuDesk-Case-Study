using System.Text.Json;
using System.Text.Json.Serialization;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

public static class CoachingPrompt
{
    public const string SystemInstructions = """
        You are an experienced, fair and constructive sales coach. You draft weekly coaching feedback
        for one team member. A manager will review and edit your draft before the team member sees it.

        You MUST:
        1. Base every statement only on the data provided. Do not use outside knowledge about the person.
        2. Cite specific evidence (a named deal, a number or a date) in the "evidence" field of every strength and improvement.
        3. Return 1-3 strengths, 1-3 improvements and 1-3 concrete actions for next week.
        4. Always include at least one genuine strength, even in a weak week.
        5. Adapt your tone to the person's tenure and preferred feedback style ("Direct" = concise and straightforward; "Encouraging" = warm and motivating).
        6. Describe behaviors and results, never personality ("did not follow up", not "is disorganized").
        7. Treat the manager note as important context (for example, time out of the office) and do not penalize the person for it.
        8. Address the person directly as "you", in English.

        You MUST NOT:
        1. Invent data, or assume causes that are not stated in the data.
        2. Comment on personal matters such as health, private life, age, gender, family or religion.
        3. Recommend HR actions such as termination, discipline, compensation or promotion.
        4. Mention or compare against other team members by name. Comparing against the team average is allowed.
        5. Use humiliating, sarcastic or threatening language.
        6. Guess when data is missing. List what you could not assess in "dataGaps" instead.
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BuildUserMessage(CoachingContext c)
    {
        var payload = new
        {
            week = new { start = c.WeekStart, end = c.WeekEnd },
            terminology = new { deal = c.DealTerm, rep = c.RepTerm },
            person = c.Rep,
            quotaAttainmentPercent = c.QuotaAttainmentPercent,
            metrics = new { thisWeek = c.Current, lastWeek = c.Previous, teamAverage = c.TeamAverage },
            pipeline = c.Deals,
            managerNote = c.ManagerNote,
        };

        return $"""
            Draft this week's coaching feedback for the {c.RepTerm.ToLowerInvariant()} described below.
            Day counts are measured up to the last day of the week. "lastWeek" is null when there is no data for the previous week.

            DATA:
            {JsonSerializer.Serialize(payload, Json)}
            """;
    }

    /// <summary>Gemini response schema (OpenAPI subset) that forces the structured output.</summary>
    public static readonly object ResponseSchema = new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["summary"] = new { type = "STRING" },
            ["strengths"] = FeedbackItems(),
            ["improvements"] = FeedbackItems(),
            ["actions"] = new
            {
                type = "ARRAY",
                minItems = 1,
                maxItems = 3,
                items = new
                {
                    type = "OBJECT",
                    properties = new Dictionary<string, object>
                    {
                        ["action"] = new { type = "STRING" },
                        ["why"] = new { type = "STRING" },
                        ["successSignal"] = new { type = "STRING" },
                    },
                    required = new[] { "action", "why", "successSignal" },
                },
            },
            ["dataGaps"] = new { type = "ARRAY", items = new { type = "STRING" } },
        },
        required = new[] { "summary", "strengths", "improvements", "actions", "dataGaps" },
    };

    /// <summary>Standard JSON Schema for the same output (Claude structured outputs). Item counts (1-3) are enforced by the parser, since the API rejects minItems/maxItems.</summary>
    public static Dictionary<string, JsonElement> JsonSchema => ToElements(new
    {
        type = "object",
        additionalProperties = false,
        properties = new Dictionary<string, object>
        {
            ["summary"] = new { type = "string" },
            ["strengths"] = StrictFeedbackItems(),
            ["improvements"] = StrictFeedbackItems(),
            ["actions"] = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    properties = new Dictionary<string, object>
                    {
                        ["action"] = new { type = "string" },
                        ["why"] = new { type = "string" },
                        ["successSignal"] = new { type = "string" },
                    },
                    required = new[] { "action", "why", "successSignal" },
                },
            },
            ["dataGaps"] = new { type = "array", items = new { type = "string" } },
        },
        required = new[] { "summary", "strengths", "improvements", "actions", "dataGaps" },
    });

    private static object StrictFeedbackItems() => new
    {
        type = "array",
        items = new
        {
            type = "object",
            additionalProperties = false,
            properties = new Dictionary<string, object>
            {
                ["title"] = new { type = "string" },
                ["detail"] = new { type = "string" },
                ["evidence"] = new { type = "string" },
            },
            required = new[] { "title", "detail", "evidence" },
        },
    };

    private static Dictionary<string, JsonElement> ToElements(object schema)
    {
        using var doc = JsonSerializer.SerializeToDocument(schema);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static object FeedbackItems() => new
    {
        type = "ARRAY",
        minItems = 1,
        maxItems = 3,
        items = new
        {
            type = "OBJECT",
            properties = new Dictionary<string, object>
            {
                ["title"] = new { type = "STRING" },
                ["detail"] = new { type = "STRING" },
                ["evidence"] = new { type = "STRING" },
            },
            required = new[] { "title", "detail", "evidence" },
        },
    };
}
