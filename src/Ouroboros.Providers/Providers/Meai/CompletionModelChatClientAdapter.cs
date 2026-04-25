// <copyright file="CompletionModelChatClientAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Wraps an Ouroboros <see cref="IChatCompletionModel"/> as a MEAI <see cref="IChatClient"/>.
/// Enables any Ouroboros provider to participate in the MEAI/Semantic Kernel ecosystem.
/// </summary>
public sealed class CompletionModelChatClientAdapter : IChatClient
{
    private readonly IChatCompletionModel _model;

    public CompletionModelChatClientAdapter(IChatCompletionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    private static readonly ChatClientMetadata MetadataValue = new(nameof(CompletionModelChatClientAdapter));

    /// <inheritdoc/>
    public ChatClientMetadata Metadata => MetadataValue;

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string prompt = ExtractPrompt(messages);
        string result = await _model.GenerateTextAsync(prompt, cancellationToken).ConfigureAwait(false);

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, result)]);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_model is IStreamingChatModel streaming)
        {
            string prompt = ExtractPrompt(messages);
            await foreach (string chunk in streaming.StreamReasoningContent(prompt, cancellationToken)
                               .ToAsyncEnumerable(cancellationToken).ConfigureAwait(false))
            {
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    Contents = [new TextContent(chunk)],
                };
            }
        }
        else
        {
            // Non-streaming fallback: generate full text and yield as single update
            string prompt = ExtractPrompt(messages);
            string result = await _model.GenerateTextAsync(prompt, cancellationToken).ConfigureAwait(false);
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(result)],
            };
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null)
    {
        if (key is not null)
        {
            return null;
        }

        if (serviceType == typeof(IChatCompletionModel))
        {
            return _model;
        }

        if (serviceType?.IsAssignableFrom(GetType()) == true)
        {
            return this;
        }

        return null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No unmanaged resources; the underlying model is not owned by this adapter.
    }

    private static string ExtractPrompt(IEnumerable<ChatMessage> messages)
    {
        // Concatenate user/system messages into a single prompt string.
        // MEAI messages can carry rich content; we extract text parts.
        var parts = new List<string>();
        foreach (var msg in messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is TextContent tc && !string.IsNullOrEmpty(tc.Text))
                {
                    parts.Add(tc.Text);
                }
            }
        }

        return parts.Count > 0 ? string.Join("\n", parts) : string.Empty;
    }
}
