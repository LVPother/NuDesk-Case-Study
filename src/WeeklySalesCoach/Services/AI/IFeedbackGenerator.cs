using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>Produces raw coaching JSON for one rep. Swappable: Gemini, demo file, or a test fake.</summary>
public interface IFeedbackGenerator
{
    bool IsLive { get; }
    string ModelName { get; }
    Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default);
}

public enum AiFailureKind { Timeout, RateLimited, Unavailable, NotConfigured, InvalidRequest }

public class AiUnavailableException(AiFailureKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public AiFailureKind Kind { get; } = kind;
}
