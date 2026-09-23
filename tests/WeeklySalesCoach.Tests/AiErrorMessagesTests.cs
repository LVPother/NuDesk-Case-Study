using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Tests;

public class AiErrorMessagesTests
{
    [Theory]
    [InlineData(AiFailureKind.Timeout, "didn't respond")]
    [InlineData(AiFailureKind.RateLimited, "usage limit")]
    [InlineData(AiFailureKind.NotConfigured, "Gemini API key")]
    [InlineData(AiFailureKind.Unavailable, "unavailable")]
    public void Maps_each_failure_kind_to_a_friendly_message(AiFailureKind kind, string expected)
    {
        Assert.Contains(expected, AiErrorMessages.For(new AiUnavailableException(kind, "internal detail")));
    }

    [Fact]
    public void Never_exposes_internal_details()
    {
        var message = AiErrorMessages.For(new AiUnavailableException(AiFailureKind.Unavailable, "secret stack info"));

        Assert.DoesNotContain("secret", message);
    }

    [Fact]
    public void Maps_invalid_content()
    {
        Assert.Contains("incomplete", AiErrorMessages.For(new InvalidCoachingContentException("x")));
    }
}
