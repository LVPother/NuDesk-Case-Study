using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class ClaudeFeedbackGeneratorTests
{
    private static ClaudeFeedbackGenerator Create(string? apiKey) => new(
        Options.Create(new ClaudeOptions { ApiKey = apiKey, Model = "claude-opus-5" }),
        NullLogger<ClaudeFeedbackGenerator>.Instance);

    [Fact]
    public async Task Missing_key_is_not_configured_and_makes_no_request()
    {
        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(" ").GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.NotConfigured, ex.Kind);
    }

    [Fact]
    public void Reports_the_configured_model_as_live()
    {
        var generator = Create("test-key");

        Assert.True(generator.IsLive);
        Assert.Equal("claude-opus-5", generator.ModelName);
    }

    [Fact]
    public void Json_schema_uses_standard_json_schema_types_and_required_fields()
    {
        var schema = CoachingPrompt.JsonSchema;
        var json = JsonSerializer.Serialize(schema);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(["summary", "strengths", "improvements", "actions", "dataGaps"],
            root.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray());
        var strengths = root.GetProperty("properties").GetProperty("strengths");
        Assert.Equal("array", strengths.GetProperty("type").GetString());
        Assert.Equal("string", strengths.GetProperty("items").GetProperty("properties").GetProperty("evidence").GetProperty("type").GetString());
        Assert.DoesNotContain("OBJECT", json);
        // Structured outputs reject minItems/maxItems; the parser enforces the 1-3 range instead.
        Assert.DoesNotContain("minItems", json);
        Assert.DoesNotContain("maxItems", json);
    }

    [Fact]
    public void Invalid_request_errors_are_not_retried_by_the_exporter_and_have_a_message()
    {
        var ex = new AiUnavailableException(AiFailureKind.InvalidRequest, "400");

        Assert.Contains("rejected", AiErrorMessages.For(ex));
        Assert.False(DemoFeedbackExporter.IsRetryable(ex));
        Assert.True(DemoFeedbackExporter.IsRetryable(new AiUnavailableException(AiFailureKind.RateLimited, "429")));
    }

    [Theory]
    [InlineData("Claude", true, true, AiProvider.Claude)]
    [InlineData("Claude", false, true, AiProvider.Gemini)]
    [InlineData("Gemini", true, true, AiProvider.Gemini)]
    [InlineData("Gemini", true, false, AiProvider.Claude)]
    [InlineData("Claude", false, false, AiProvider.Demo)]
    [InlineData("", true, false, AiProvider.Claude)]
    public void Provider_selection_prefers_the_configured_provider_then_any_live_one_then_demo(
        string preferred, bool claudeConfigured, bool geminiConfigured, AiProvider expected)
    {
        Assert.Equal(expected, AiProviderSelector.Choose(preferred, claudeConfigured, geminiConfigured));
    }
}
