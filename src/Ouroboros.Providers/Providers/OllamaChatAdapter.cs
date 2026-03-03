using System.Reactive.Linq;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;

namespace Ouroboros.Providers;

/// <summary>
/// Adapter for local Ollama models via OllamaSharp. We attempt to call the SDK when available,
/// falling back to a deterministic stub when the local daemon is not reachable.
/// Supports thinking mode for models like DeepSeek R1 that emit &lt;think&gt; tags.
/// </summary>
public sealed class OllamaChatAdapter : IStreamingThinkingChatModel
{
    private readonly OllamaApiClient _client;
    private readonly string _modelName;
    private readonly string? _culture;
    private RequestOptions? _options;

    public OllamaChatAdapter(OllamaApiClient client, string modelName, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _client = client;
        _modelName = modelName;
        _culture = culture;
    }

    /// <summary>
    /// Gets or sets the <see cref="RequestOptions"/> applied to every generate request.
    /// </summary>
    public RequestOptions? Options
    {
        get => _options;
        set => _options = value;
    }

    /// <summary>
    /// Gets or sets the optional KeepAlive duration applied to every generate request.
    /// Uses Ollama duration format (e.g. "10m", "5m", "0" to unload immediately).
    /// This is separate from <see cref="Options"/> because KeepAlive lives on
    /// <see cref="GenerateRequest"/>, not on <see cref="RequestOptions"/>.
    /// </summary>
    public string? KeepAlive { get; set; }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;
            var sb = new StringBuilder();

            var request = new GenerateRequest
            {
                Model = _modelName,
                Prompt = finalPrompt,
                Stream = true,
                Options = _options
            };

            if (KeepAlive is not null)
            {
                request.KeepAlive = KeepAlive;
            }

            await foreach (GenerateResponseStream? chunk in _client.GenerateAsync(request, ct).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk?.Response))
                {
                    sb.Append(chunk.Response);
                }
            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return string.Empty;
        }
        catch
        {
            // Deterministic fallback keeps the pipeline running in offline scenarios.
            return $"[ollama-fallback:{_modelName}] {prompt}";
        }
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        string rawText = await GenerateTextAsync(prompt, ct);
        return ThinkingResponse.FromRawText(rawText);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

                var request = new GenerateRequest
                {
                    Model = _modelName,
                    Prompt = finalPrompt,
                    Stream = true,
                    Options = _options
                };

                if (KeepAlive is not null)
                {
                    request.KeepAlive = KeepAlive;
                }

                bool inThinking = false;
                StringBuilder buffer = new();

                await foreach (GenerateResponseStream? chunk in _client.GenerateAsync(request, token).ConfigureAwait(false))
                {
                    string text = chunk?.Response ?? string.Empty;
                    if (string.IsNullOrEmpty(text)) continue;

                    buffer.Append(text);
                    string bufferStr = buffer.ToString();

                    // Check for thinking tag transitions
                    if (!inThinking && bufferStr.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = bufferStr.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                        string beforeTag = bufferStr[..idx];
                        if (!string.IsNullOrEmpty(beforeTag))
                            observer.OnNext((false, beforeTag));

                        buffer.Clear();
                        buffer.Append(bufferStr[(idx + 7)..]);
                        inThinking = true;
                        continue;
                    }

                    if (inThinking && bufferStr.Contains("</think>", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = bufferStr.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                        string thinkingContent = bufferStr[..idx];
                        if (!string.IsNullOrEmpty(thinkingContent))
                            observer.OnNext((true, thinkingContent));

                        buffer.Clear();
                        buffer.Append(bufferStr[(idx + 8)..]);
                        inThinking = false;
                        continue;
                    }

                    // Emit chunk with current state
                    observer.OnNext((inThinking, text));
                    buffer.Clear();
                }

                // Flush any remaining buffer
                if (buffer.Length > 0)
                    observer.OnNext((inThinking, buffer.ToString()));

                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;

                var request = new GenerateRequest
                {
                    Model = _modelName,
                    Prompt = finalPrompt,
                    Stream = true,
                    Options = _options
                };

                if (KeepAlive is not null)
                {
                    request.KeepAlive = KeepAlive;
                }

                await foreach (GenerateResponseStream? chunk in _client.GenerateAsync(request, token).ConfigureAwait(false))
                {
                    string text = chunk?.Response ?? string.Empty;
                    if (!string.IsNullOrEmpty(text))
                    {
                        observer.OnNext(text);
                    }
                }
                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }
}
