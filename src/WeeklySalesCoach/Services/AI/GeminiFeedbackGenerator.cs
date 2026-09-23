using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.Coaching;

namespace WeeklySalesCoach.Services.AI;

public class GeminiFeedbackGenerator(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiFeedbackGenerator> logger)
    : IFeedbackGenerator
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    public bool IsLive => true;
    public string ModelName => options.Value.Model;

    public async Task<string> GenerateAsync(CoachingContext context, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new AiUnavailableException(AiFailureKind.NotConfigured, "Gemini API key is not configured.");

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = CoachingPrompt.SystemInstructions } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = CoachingPrompt.BuildUserMessage(context) } } } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = CoachingPrompt.ResponseSchema,
                temperature = 0.4,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{settings.Model}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        // The key travels in a header, never in the URL, so it can't leak into logs.
        request.Headers.Add("x-goog-api-key", settings.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AiUnavailableException(AiFailureKind.Timeout, "Gemini did not respond in time.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiUnavailableException(AiFailureKind.Unavailable, "Could not reach Gemini.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new AiUnavailableException(AiFailureKind.RateLimited, "Gemini rate limit reached.");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Gemini returned {Status}: {Body}", (int)response.StatusCode, error.Length > 500 ? error[..500] : error);
                throw new AiUnavailableException(AiFailureKind.Unavailable, $"Gemini returned {(int)response.StatusCode}.");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return ExtractText(doc.RootElement);
        }
    }

    private static string ExtractText(JsonElement root)
    {
        if (root.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array)
        {
            var text = string.Concat(parts.EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        throw new InvalidCoachingContentException("The AI response contained no content.");
    }
}
