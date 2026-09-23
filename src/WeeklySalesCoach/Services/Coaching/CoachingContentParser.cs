using System.Text.Json;

namespace WeeklySalesCoach.Services.Coaching;

public static class CoachingContentParser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Parses raw AI output and enforces the coaching rules. Throws <see cref="InvalidCoachingContentException"/>.</summary>
    public static CoachingContent Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidCoachingContentException("The AI response was empty.");

        CoachingContent? content;
        try
        {
            content = JsonSerializer.Deserialize<CoachingContent>(StripCodeFences(raw), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidCoachingContentException("The AI response was not valid JSON.", ex);
        }

        if (content is null)
            throw new InvalidCoachingContentException("The AI response was empty.");

        Normalize(content);
        TrimToLimit(content);
        Validate(content);
        return content;
    }

    /// <summary>AI output occasionally carries a 4th item; keep the first three rather than reject the whole draft.</summary>
    private static void TrimToLimit(CoachingContent c)
    {
        if (c.Strengths.Count > MaxItems) c.Strengths = c.Strengths.Take(MaxItems).ToList();
        if (c.Improvements.Count > MaxItems) c.Improvements = c.Improvements.Take(MaxItems).ToList();
        if (c.Actions.Count > MaxItems) c.Actions = c.Actions.Take(MaxItems).ToList();
    }

    public const int MaxItems = 3;

    public static void Validate(CoachingContent content)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(content.Summary)) errors.Add("A summary is required.");
        CheckCount("strengths", content.Strengths?.Count ?? 0, errors);
        CheckCount("improvements", content.Improvements?.Count ?? 0, errors);
        CheckCount("actions", content.Actions?.Count ?? 0, errors);

        foreach (var item in (content.Strengths ?? []).Concat(content.Improvements ?? []))
        {
            if (string.IsNullOrWhiteSpace(item.Title)) errors.Add("Every strength and improvement needs a title.");
            if (string.IsNullOrWhiteSpace(item.Evidence)) errors.Add("Every strength and improvement must cite evidence.");
        }

        foreach (var action in content.Actions ?? [])
        {
            if (string.IsNullOrWhiteSpace(action.Action)) errors.Add("Every action needs a description.");
        }

        if (errors.Count > 0)
            throw new InvalidCoachingContentException(string.Join(" ", errors.Distinct()));
    }

    public static string Serialize(CoachingContent content) => JsonSerializer.Serialize(content, Json);

    /// <summary>Reads content that was already validated when it was saved.</summary>
    public static CoachingContent FromStored(string json) =>
        Normalize(JsonSerializer.Deserialize<CoachingContent>(json, Json) ?? new CoachingContent());

    private static void CheckCount(string name, int count, List<string> errors)
    {
        if (count is < 1 or > MaxItems) errors.Add($"Expected 1-{MaxItems} {name}, got {count}.");
    }

    private static CoachingContent Normalize(CoachingContent c)
    {
        c.Summary = Unescape(c.Summary ?? "");
        c.Strengths ??= [];
        c.Improvements ??= [];
        c.Actions ??= [];
        c.DataGaps = (c.DataGaps ?? []).Select(Unescape).ToList();
        foreach (var item in c.Strengths.Concat(c.Improvements))
        {
            item.Title = Unescape(item.Title);
            item.Detail = Unescape(item.Detail);
            item.Evidence = Unescape(item.Evidence);
        }
        foreach (var action in c.Actions)
        {
            action.Action = Unescape(action.Action);
            action.Why = Unescape(action.Why);
            action.SuccessSignal = Unescape(action.SuccessSignal);
        }
        return c;
    }

    private static readonly System.Text.RegularExpressions.Regex DoubleEscapedUnicode =
        new(@"\\u([0-9a-fA-F]{4})", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Some models double-escape inside structured output, so a string arrives containing a literal
    /// "—" or "\"". Turn those back into the intended characters; plain text is left untouched.
    /// </summary>
    private static string Unescape(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('\\')) return value ?? "";
        var text = DoubleEscapedUnicode.Replace(value, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        return text.Replace("\\\"", "\"");
    }

    private static string StripCodeFences(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;

        var firstNewline = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline
            ? text[(firstNewline + 1)..lastFence].Trim()
            : text;
    }
}
