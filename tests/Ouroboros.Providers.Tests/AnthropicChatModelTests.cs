#pragma warning disable CA2000 // HttpResponseMessage ownership follows HttpClient; AnthropicClient is test-owned with HttpClient disposal.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Anthropic;
using FluentAssertions;
using Xunit;

namespace Ouroboros.Tests;

public sealed class AnthropicChatModelTests
{
    private const string SecretPromptMarker = "SECRET_PROMPT_MARKER_DO_NOT_LEAK";

    private sealed class AnthropicJsonHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public AnthropicJsonHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_OnSuccess_InvokesCostTrackerWithUsage()
    {
        using var handler = new AnthropicJsonHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": "msg_test",
                  "type": "message",
                  "role": "assistant",
                  "content": [ { "type": "text", "text": "ok" } ],
                  "model": "claude-3-5-haiku-20241022",
                  "stop_reason": "end_turn",
                  "usage": { "input_tokens": 10, "output_tokens": 5 }
                }
                """,
                Encoding.UTF8,
                "application/json"),
        });

        using var http = new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri("https://api.anthropic.com") };
        var client = new AnthropicClient { ApiKey = "sk-ant-test", HttpClient = http };
        var tracker = new LlmCostTracker("claude-3-5-haiku-20241022", "Anthropic");
        SessionMetrics before = tracker.GetSessionMetrics();

        using var model = new AnthropicChatModel(client, "claude-3-5-haiku-20241022", settings: null, thinkingBudgetTokens: null, costTracker: tracker);
        ThinkingResponse response = await model.GenerateWithThinkingAsync("hello", CancellationToken.None);

        response.Content.Should().Be("ok");
        SessionMetrics after = tracker.GetSessionMetrics();
        after.TotalRequests.Should().BeGreaterThan(before.TotalRequests);
        after.TotalInputTokens.Should().BeGreaterThanOrEqualTo(10);
        after.TotalOutputTokens.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_WhenCanceled_PropagatesOperationCanceledException()
    {
        using var handler = new AnthropicJsonHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
        using var http = new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri("https://api.anthropic.com") };
        var client = new AnthropicClient { ApiKey = "sk-ant-test", HttpClient = http };
        using var model = new AnthropicChatModel(client, "claude-3-5-haiku-20241022", null, null, null);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = async () => await model.GenerateWithThinkingAsync("x", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_OnApiError_DoesNotEchoUserPrompt()
    {
        using var handler = new AnthropicJsonHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = new { type = "invalid_request_error", message = "bad" } }),
                Encoding.UTF8,
                "application/json"),
        });

        using var http = new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri("https://api.anthropic.com") };
        var client = new AnthropicClient { ApiKey = "sk-ant-test", HttpClient = http };
        using var model = new AnthropicChatModel(client, "claude-3-5-haiku-20241022", null, null, null);

        ThinkingResponse response = await model.GenerateWithThinkingAsync(SecretPromptMarker, CancellationToken.None);

        response.Content.Should().NotContain(SecretPromptMarker);
    }
}
