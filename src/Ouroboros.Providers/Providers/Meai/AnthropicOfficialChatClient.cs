// <copyright file="AnthropicOfficialChatClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// MEAI <see cref="IChatClient"/> surface backed by the official Anthropic SDK via <see cref="AnthropicChatModel"/>.
/// </summary>
/// <remarks>
/// The Anthropic NuGet package does not ship a separate Microsoft.Extensions.AI bridge package compatible with
/// this repo&apos;s pinned MEAI version; this thin adapter delegates to <see cref="CompletionModelChatClientAdapter"/>
/// so streaming and non-streaming paths stay aligned with other Ouroboros chat models.
/// </remarks>
public sealed class AnthropicOfficialChatClient : IChatClient
{
    private readonly CompletionModelChatClientAdapter _inner;
    private static readonly ChatClientMetadata s_metadata = new(nameof(AnthropicOfficialChatClient));

    /// <summary>
    /// Initializes a new instance wrapping <paramref name="model"/>.
    /// </summary>
    /// <param name="model">Configured Anthropic chat model.</param>
    public AnthropicOfficialChatClient(AnthropicChatModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _inner = new CompletionModelChatClientAdapter(model);
    }

    /// <inheritdoc/>
    public ChatClientMetadata Metadata => s_metadata;

    /// <inheritdoc/>
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetResponseAsync(messages, options, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null)
    {
        if (key is null && serviceType == typeof(IChatClient))
            return this;

        return _inner.GetService(serviceType, key);
    }

    /// <inheritdoc/>
    public void Dispose() => _inner.Dispose();
}
