namespace WeeklySalesCoach.Configuration;

/// <summary>Industry-specific wording. Changing industries means changing these values and the seed, not the code.</summary>
public class DomainOptions
{
    public const string Section = "Domain";
    public string DealTerm { get; set; } = "Deal";
    public string RepTerm { get; set; } = "Sales Rep";
    public string WonStage { get; set; } = "Won";
    public string LostStage { get; set; } = "Lost";
}

/// <summary>Which live AI provider to prefer. Falls back to any configured provider, then demo mode.</summary>
public class AiOptions
{
    public const string Section = "Ai";
    public string Provider { get; set; } = "Claude";
}

public class ClaudeOptions
{
    public const string Section = "Claude";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-haiku-4-5";
    public int TimeoutSeconds { get; set; } = 120;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class GeminiOptions
{
    public const string Section = "Gemini";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gemini-3.6-flash";
    public int TimeoutSeconds { get; set; } = 30;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class DemoOptions
{
    public const string Section = "Demo";
    public bool EnableQuickLogin { get; set; } = true;
    public string DemoFeedbackPath { get; set; } = "Data/Seed/demo-feedback.json";
}
