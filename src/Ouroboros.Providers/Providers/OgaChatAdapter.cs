// <copyright file="OgaChatAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using Ouroboros.Abstractions.Core;
using R3;

namespace Ouroboros.Providers;

/// <summary>
/// Adapter for local ONNX Runtime GenAI (OGA) models.
/// Wraps <see cref="OnnxRuntimeGenAIChatClient"/> and exposes it through
/// Ouroboros chat abstractions (<see cref="IOuroborosChatClient"/>,
/// <see cref="IChatClientBridge"/>, <see cref="ILlmProvider"/>).
/// </summary>
/// <remarks>
/// Supports any OGA-compatible ONNX model (e.g. Hermes-4.3-36B INT4).
/// The model path must point to a directory containing
/// <c>model.onnx</c>, <c>model.onnx.data</c>, and <c>genai_config.json</c>.
/// </remarks>
public sealed class OgaChatAdapter : IOuroborosChatClient, IChatCompletionModel, IChatClientBridge, ILlmProvider, IDisposable
{
    private readonly OnnxRuntimeGenAIChatClient _client;
    private readonly string? _culture;
    private readonly MeTTaPromptCompressor? _compressor;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OgaChatAdapter"/> class.
    /// </summary>
    /// <param name="modelPath">Path to the OGA model directory.</param>
    /// <param name="culture">Optional culture hint for responses.</param>
    /// <param name="compressor">Optional MeTTa prompt compressor for oversized prompts.</param>
    public OgaChatAdapter(string modelPath, string? culture = null, MeTTaPromptCompressor? compressor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _client = new OnnxRuntimeGenAIChatClient(modelPath);
        _culture = culture;
        _compressor = compressor;
    }

    /// <inheritdoc/>
    public bool SupportsThinking => false;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <summary>
    /// Gets or sets the maximum prompt size in bytes before truncation.
    /// </summary>
    public int MaxPromptBytes { get; set; } = 90_000;

    /// <inheritdoc/>
    public Task<string> GenerateAsync(string prompt) =>
        GenerateTextAsync(prompt, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string finalPrompt = _culture is { Length: > 0 } c
            ? $"Please answer in {c}. {prompt}"
            : prompt;

        // Guard: compress or truncate oversized prompts
        int promptBytes = Encoding.UTF8.GetByteCount(finalPrompt);
        if (promptBytes > MaxPromptBytes)
        {
            if (_compressor is not null)
            {
                Console.Error.WriteLine(
                    $"  [OGA] Prompt too large ({promptBytes / 1024}KB > {MaxPromptBytes / 1024}KB) — compressing via MeTTa...");
                finalPrompt = _compressor.CompressAsync(finalPrompt, MaxPromptBytes, ct).GetAwaiter().GetResult();
            }
            else
            {
                int charLimit = MaxPromptBytes * 3 / 4;
                if (charLimit < finalPrompt.Length)
                {
                    Console.Error.WriteLine(
                        $"  [OGA] Prompt too large ({promptBytes / 1024}KB > {MaxPromptBytes / 1024}KB) — truncating to {charLimit} chars");
                    finalPrompt = finalPrompt[..charLimit];
                }
            }
        }

        try
        {
            var messages = new List<ChatMessage> { new(ChatRole.User, finalPrompt) };
            var response = await _client.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"  [OGA] Inference error: {ex.Message}");
            return $"I'm having trouble with the local ONNX model right now. ({ex.Message})";
        }
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        string rawText = await GenerateTextAsync(prompt, ct).ConfigureAwait(false);
        return ThinkingResponse.FromRawText(rawText);
    }

    /// <inheritdoc/>
    public Observable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _culture is { Length: > 0 } c
                    ? $"Please answer in {c}. {prompt}"
                    : prompt;

                var messages = new List<ChatMessage> { new(ChatRole.User, finalPrompt) };
                await foreach (var update in _client.GetStreamingResponseAsync(messages, cancellationToken: token).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        observer.OnNext((false, update.Text));
                    }
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnErrorResume(ex);
            }
        });
    }

    /// <inheritdoc/>
    public Observable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _culture is { Length: > 0 } c
                    ? $"Please answer in {c}. {prompt}"
                    : prompt;

                var messages = new List<ChatMessage> { new(ChatRole.User, finalPrompt) };
                await foreach (var update in _client.GetStreamingResponseAsync(messages, cancellationToken: token).ConfigureAwait(false))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        observer.OnNext(update.Text);
                    }
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnErrorResume(ex);
            }
        });
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await foreach (var update in _client.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <inheritdoc/>
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _client.GetResponseAsync(messages, options, cancellationToken);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is not null)
            return null;

        return serviceType == typeof(ChatClientMetadata)
            ? (_client as IChatClient)?.GetService(typeof(ChatClientMetadata))
            : serviceType?.IsInstanceOfType(this) is true ? this : null;
    }

    /// <inheritdoc/>
    public IChatClient GetChatClient() => _client;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _client.Dispose();
        _disposed = true;
    }
}
