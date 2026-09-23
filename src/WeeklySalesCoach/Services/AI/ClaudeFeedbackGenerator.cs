using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

/// <summary>Live generator on the Claude API (official Anthropic SDK) with structured JSON output.</summary>
public class ClaudeFeedbackGenerator(IOptions<ClaudeOptions> options, ILogger<ClaudeFeedbackGenerator> logger)
    : IFeedbackGenerator
{
    public bool IsLive => true;
    public string ModelName => options.Value.Model;

    public async Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new AiUnavailableException(AiFailureKind.NotConfigured, "Claude API key is not configured.");

        var client = new AnthropicClient
        {
            ApiKey = settings.ApiKey,
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
        };

        var request = new MessageCreateParams
        {
            Model = settings.Model,
            MaxTokens = 4096,
            System = CoachingPrompt.SystemInstructions,
            Messages = [new() { Role = Role.User, Content = CoachingPrompt.BuildUserMessage(context) }],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = CoachingPrompt.JsonSchema },
            },
        };

        Message response;
        try
        {
            response = await client.Messages.Create(request, cancellationToken: ct);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new AiUnavailableException(AiFailureKind.RateLimited, "Claude rate limit reached.", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new AiUnavailableException(AiFailureKind.Unavailable, "Claude is temporarily unavailable.", ex);
        }
        catch (Anthropic4xxException ex)
        {
            // Bad schema, bad model name, invalid key: retrying won't help.
            logger.LogWarning(ex, "Claude rejected the request");
            throw new AiUnavailableException(AiFailureKind.InvalidRequest, "Claude rejected the request.", ex);
        }
        catch (AnthropicApiException ex)
        {
            logger.LogWarning(ex, "Claude request failed");
            throw new AiUnavailableException(AiFailureKind.Unavailable, "Claude returned an error.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AiUnavailableException(AiFailureKind.Timeout, "Claude did not respond in time.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiUnavailableException(AiFailureKind.Unavailable, "Could not reach Claude.", ex);
        }

        if (response.StopReason?.ToString() == "refusal")
            throw new AiUnavailableException(AiFailureKind.Unavailable, "Claude declined to produce this report.");

        var text = string.Concat(response.Content.Select(b => b.TryPickText(out var t) ? t.Text : null));
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidCoachingContentException("The AI response contained no content.");

        return text;
    }
}
