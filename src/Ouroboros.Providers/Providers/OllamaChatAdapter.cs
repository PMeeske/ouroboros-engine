using System.Reactive.Linq;
using LangChain.Providers.Ollama;

namespace Ouroboros.Providers;

/// <summary>
/// Adapter for local Ollama models. We attempt to call the SDK when available,
/// falling back to a deterministic stub when the local daemon is not reachable.
/// Supports thinking mode for models like DeepSeek R1 that emit &lt;think&gt; tags.
/// </summary>
public sealed class OllamaChatAdapter : IStreamingThinkingChatModel
{
    private readonly OllamaChatModel _model;
    private readonly string? _culture;

    public OllamaChatAdapter(OllamaChatModel model, string? culture = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _culture = culture;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;
            IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(finalPrompt, cancellationToken: ct);
            StringBuilder builder = new StringBuilder();

            await foreach (LangChain.Providers.ChatResponse? chunk in stream.WithCancellation(ct).ConfigureAwait(false))
            {
                string text = ExtractResponseText(chunk);
                if (!string.IsNullOrEmpty(text))
                {
                    builder.Append(text);
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }

            return ExtractResponseText(null);
        }
        catch
        {
            // Deterministic fallback keeps the pipeline running in offline scenarios.
            return $"[ollama-fallback:{_model.GetType().Name}] {prompt}";
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
                IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(finalPrompt, cancellationToken: token);

                bool inThinking = false;
                StringBuilder buffer = new();

                await foreach (LangChain.Providers.ChatResponse? chunk in stream.WithCancellation(token).ConfigureAwait(false))
                {
                    string text = ExtractResponseText(chunk);
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
                IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(finalPrompt, cancellationToken: token);
                await foreach (LangChain.Providers.ChatResponse? chunk in stream.WithCancellation(token).ConfigureAwait(false))
                {
                    string text = ExtractResponseText(chunk);
                    if (!string.IsNullOrEmpty(text))
                    {
                        observer.OnNext(text);
                    }
                }
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    private static string ExtractResponseText(object? response)
    {
        if (response is null) return string.Empty;

        switch (response)
        {
            case string s:
                return s;
            case IEnumerable<string> strings:
                return string.Join(Environment.NewLine, strings);
        }

        Type type = response.GetType();

        System.Reflection.PropertyInfo? lastMessageProperty = type.GetProperty("LastMessageContent");
        if (lastMessageProperty?.GetValue(response) is string last)
        {
            return last;
        }

        System.Reflection.PropertyInfo? contentProperty = type.GetProperty("Content");
        if (contentProperty?.GetValue(response) is string content)
        {
            return content;
        }

        System.Reflection.PropertyInfo? messageProperty = type.GetProperty("Message");
        if (messageProperty?.GetValue(response) is { } message)
        {
            if (message is string mString)
            {
                return mString;
            }

            if (message is IEnumerable<string> enumerable)
            {
                return string.Join(Environment.NewLine, enumerable);
            }

            string? nestedContent = message.GetType().GetProperty("Content")?.GetValue(message) as string;
            if (!string.IsNullOrWhiteSpace(nestedContent))
            {
                return nestedContent!;
            }
        }

        return response.ToString() ?? string.Empty;
    }
}