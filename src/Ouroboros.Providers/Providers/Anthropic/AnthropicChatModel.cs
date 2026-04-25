using System.Net.Http;
using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using R3;

namespace Ouroboros.Providers;

/// <summary>
/// Chat model adapter for Anthropic Claude API using the official Anthropic .NET SDK.
/// Supports streaming, extended thinking, and Claude models referenced by string id.
/// </summary>
public sealed class AnthropicChatModel : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly int? _thinkingBudgetTokens;
    private readonly LlmCostTracker? _costTracker;

    /// <summary>
    /// Gets the cost tracker for this model instance.
    /// </summary>
    public LlmCostTracker? CostTracker => _costTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicChatModel"/> class.
    /// </summary>
    /// <param name="apiKey">Anthropic API key (sk-ant-...).</param>
    /// <param name="model">Model name (e.g., claude-opus-4-5-20251101, claude-sonnet-4-20250514).</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <param name="thinkingBudgetTokens">Optional token budget for extended thinking (requires compatible model).</param>
    /// <param name="costTracker">Optional cost tracker for monitoring usage and costs.</param>
    public AnthropicChatModel(string apiKey, string model, ChatRuntimeSettings? settings = null, int? thinkingBudgetTokens = null, LlmCostTracker? costTracker = null)
        : this(CreateClient(apiKey), model, settings, thinkingBudgetTokens, costTracker)
    {
    }

    private static AnthropicClient CreateClient(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required", nameof(apiKey));
        }

        return new AnthropicClient { ApiKey = apiKey };
    }

    /// <summary>
    /// Creates an AnthropicChatModel from environment variables.
    /// Uses ANTHROPIC_API_KEY environment variable.
    /// </summary>
    /// <param name="model">Model name.</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <param name="thinkingBudgetTokens">Optional token budget for extended thinking.</param>
    /// <param name="costTracker">Optional cost tracker for monitoring usage and costs.</param>
    /// <returns>Configured AnthropicChatModel instance.</returns>
    public static AnthropicChatModel FromEnvironment(string model, ChatRuntimeSettings? settings = null, int? thinkingBudgetTokens = null, LlmCostTracker? costTracker = null)
    {
        string? apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set");
        }

        return new AnthropicChatModel(apiKey, model, settings, thinkingBudgetTokens, costTracker);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicChatModel"/> class.
    /// Internal constructor for tests that need a custom <see cref="AnthropicClient"/> (for example HTTP stubs).
    /// </summary>
    internal AnthropicChatModel(AnthropicClient client, string model, ChatRuntimeSettings? settings, int? thinkingBudgetTokens, LlmCostTracker? costTracker)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        _thinkingBudgetTokens = thinkingBudgetTokens;
        _costTracker = costTracker ?? new LlmCostTracker(model, "Anthropic");
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        ThinkingResponse response = await GenerateWithThinkingAsync(prompt, ct).ConfigureAwait(false);
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        _costTracker?.StartRequest();
        try
        {
            string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

            MessageCreateParams parameters = BuildMessageCreateParams(finalPrompt, stream: false);

            Message result = await _client.Messages.Create(parameters, cancellationToken: ct).ConfigureAwait(false);

            int inputTokens = (int)(result.Usage?.InputTokens ?? 0L);
            int outputTokens = (int)(result.Usage?.OutputTokens ?? 0L);
            _costTracker?.EndRequest(inputTokens, outputTokens);

            string? thinking = null;
            StringBuilder contentBuilder = new();
            if (result.Content is not null)
            {
                foreach (ContentBlock block in result.Content)
                {
                    if (block.TryPickThinking(out ThinkingBlock thinkingBlock))
                    {
                        thinking = thinkingBlock.Thinking;
                    }
                    else if (block.TryPickText(out TextBlock textBlock))
                    {
                        contentBuilder.Append(textBlock.Text);
                    }
                }
            }

            string content = contentBuilder.ToString();

            if (!string.IsNullOrEmpty(thinking))
            {
                return new ThinkingResponse(thinking, content, inputTokens, outputTokens);
            }

            if (!string.IsNullOrEmpty(content))
            {
                ThinkingResponse parsed = ThinkingResponse.FromRawText(content);
                return parsed with { ThinkingTokens = inputTokens, ContentTokens = outputTokens };
            }

            return new ThinkingResponse(null, content);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Trace.TraceWarning("[AnthropicChatModel] Error: {0}: {1}", ex.GetType().Name, ex.Message);
            Console.Error.WriteLine($"  [AnthropicChatModel] API error ({_model}): {ex.Message}");
            return new ThinkingResponse(null, $"I'm having trouble reaching my thinking backend right now. ({ex.Message})");
        }
        catch (AnthropicException ex)
        {
            System.Diagnostics.Trace.TraceWarning("[AnthropicChatModel] Error: {0}: {1}", ex.GetType().Name, ex.Message);
            Console.Error.WriteLine($"  [AnthropicChatModel] API error ({_model}): {ex.Message}");
            return new ThinkingResponse(null, $"I'm having trouble reaching my thinking backend right now. ({ex.Message})");
        }
    }

    /// <inheritdoc/>
    public Observable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
            CancellationToken streamCt = linked.Token;
            try
            {
                string finalPrompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

                MessageCreateParams parameters = BuildMessageCreateParams(finalPrompt, stream: true);

                IAsyncEnumerable<RawMessageStreamEvent> stream = _client.Messages.CreateStreaming(parameters, cancellationToken: streamCt);
                await foreach (RawMessageStreamEvent rawEvent in stream.ConfigureAwait(false))
                {
                    if (!rawEvent.TryPickContentBlockDelta(out RawContentBlockDeltaEvent? deltaEvent))
                    {
                        continue;
                    }

                    if (deltaEvent.Delta.TryPickText(out TextDelta? textDelta) &&
                        textDelta.Text is { Length: > 0 } t)
                    {
                        observer.OnNext((false, t));
                    }
                    else if (deltaEvent.Delta.TryPickThinking(out ThinkingDelta? thinkingDelta) &&
                        thinkingDelta.Thinking is { Length: > 0 } th)
                    {
                        observer.OnNext((true, th));
                    }
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException) when (streamCt.IsCancellationRequested)
            {
                observer.OnCompleted();
            }
            catch (HttpRequestException ex)
            {
                observer.OnErrorResume(ex);
            }
            catch (AnthropicException ex)
            {
                observer.OnErrorResume(ex);
            }
        });
    }

    /// <inheritdoc/>
    public Observable<string> StreamReasoningContent(string prompt, CancellationToken ct = default) =>
        StreamWithThinkingAsync(prompt, ct).Select(tuple => tuple.Chunk);

    private MessageCreateParams BuildMessageCreateParams(string finalPrompt, bool stream)
    {
        _ = stream; // Streaming uses CreateStreaming with the same params shape (no Stream flag on params).

        if (_thinkingBudgetTokens is > 0)
        {
            return new MessageCreateParams
            {
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = finalPrompt,
                    },
                ],
                MaxTokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 128_000,
                Model = _model,
                Thinking = new ThinkingConfigEnabled { BudgetTokens = _thinkingBudgetTokens.Value },
            };
        }

        return new MessageCreateParams
        {
            Messages =
            [
                new MessageParam
                {
                    Role = Role.User,
                    Content = finalPrompt,
                },
            ],
            MaxTokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 128_000,
            Model = _model,
        };
    }

    public void Dispose()
    {
        // Official AnthropicClient does not expose IDisposable; nothing to release.
    }
}
