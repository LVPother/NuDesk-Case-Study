using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Tests.TestSupport;

/// <summary>Returns the queued responses in order. A response may throw to simulate failures.</summary>
public sealed class FakeFeedbackGenerator(params Func<string>[] responses) : IFeedbackGenerator
{
    private readonly Queue<Func<string>> _responses = new(responses);

    public int Calls { get; private set; }
    public bool IsLive { get; init; }
    public string ModelName => "fake-model";

    public Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(_responses.Dequeue()());
    }
}
