using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>User-facing text for AI failures. Never includes exception details.</summary>
public static class AiErrorMessages
{
    public static string For(Exception ex) => ex switch
    {
        AiUnavailableException { Kind: AiFailureKind.Timeout } => "The AI didn't respond in time. Please try again.",
        AiUnavailableException { Kind: AiFailureKind.RateLimited } => "AI usage limit reached. Please wait a minute and try again.",
        AiUnavailableException { Kind: AiFailureKind.NotConfigured } => "Live AI isn't configured. Add a Claude or Gemini API key to generate live (see README).",
        AiUnavailableException { Kind: AiFailureKind.InvalidRequest } => "The AI rejected the request. Check the model name and API key in the configuration.",
        AiUnavailableException => "The AI service is unavailable right now. Please try again.",
        InvalidCoachingContentException => "The AI returned an incomplete report twice. Please try again.",
        _ => "Something went wrong. Please try again.",
    };
}
