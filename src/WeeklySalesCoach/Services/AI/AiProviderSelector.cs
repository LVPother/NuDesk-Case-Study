namespace WeeklySalesCoach.Services.AI;

public enum AiProvider { Demo, Claude, Gemini }

/// <summary>Picks the live provider: the preferred one when it has a key, otherwise any configured one, otherwise demo.</summary>
public static class AiProviderSelector
{
    public static AiProvider Choose(string? preferred, bool claudeConfigured, bool geminiConfigured)
    {
        var wantsGemini = string.Equals(preferred, "Gemini", StringComparison.OrdinalIgnoreCase);
        if (wantsGemini && geminiConfigured) return AiProvider.Gemini;
        if (!wantsGemini && claudeConfigured) return AiProvider.Claude;
        if (claudeConfigured) return AiProvider.Claude;
        if (geminiConfigured) return AiProvider.Gemini;
        return AiProvider.Demo;
    }
}
