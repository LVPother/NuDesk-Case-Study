using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeeklySalesCoach.Configuration;
using WeeklySalesCoach.Services.AI;
using WeeklySalesCoach.Services.Coaching;
using WeeklySalesCoach.Tests.TestSupport;

namespace WeeklySalesCoach.Tests;

public class GeminiFeedbackGeneratorTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string GeminiBody(string text) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text } } } } },
    });

    private static GeminiFeedbackGenerator Create(StubHandler handler, string? apiKey = "test-key") => new(
        new HttpClient(handler),
        Options.Create(new GeminiOptions { ApiKey = apiKey, Model = "gemini-test" }),
        NullLogger<GeminiFeedbackGenerator>.Instance);

    [Fact]
    public async Task Returns_text_and_sends_key_in_header_with_schema()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, GeminiBody(TestContent.ValidJson)));

        var result = await Create(handler).GenerateAsync(TestContent.SampleContext());

        Assert.Equal(TestContent.ValidJson, result);
        Assert.Equal("test-key", handler.Request!.Headers.GetValues("x-goog-api-key").Single());
        Assert.Contains("models/gemini-test:generateContent", handler.Request.RequestUri!.ToString());
        Assert.DoesNotContain("test-key", handler.Request.RequestUri.ToString());
        Assert.Contains("\"responseMimeType\":\"application/json\"", handler.RequestBody);
        Assert.Contains("\"responseSchema\"", handler.RequestBody);
        Assert.Contains("Ana Torres", handler.RequestBody);
    }

    [Fact]
    public async Task Maps_429_to_rate_limited()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.TooManyRequests, "{}"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.RateLimited, ex.Kind);
    }

    [Fact]
    public async Task Maps_server_errors_to_unavailable()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Maps_timeouts_to_timeout()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timed out"));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Response_without_candidates_is_invalid_content()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<InvalidCoachingContentException>(() => Create(handler).GenerateAsync(TestContent.SampleContext()));
    }

    [Fact]
    public async Task Missing_key_is_not_configured_and_makes_no_request()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, GeminiBody(TestContent.ValidJson)));

        var ex = await Assert.ThrowsAsync<AiUnavailableException>(() => Create(handler, apiKey: " ").GenerateAsync(TestContent.SampleContext()));

        Assert.Equal(AiFailureKind.NotConfigured, ex.Kind);
        Assert.Equal(0, handler.Calls);
    }
}
