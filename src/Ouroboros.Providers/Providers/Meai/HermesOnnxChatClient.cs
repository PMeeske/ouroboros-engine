// <copyright file="HermesOnnxChatClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Ouroboros.Providers.HermesOnnx;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// MEAI <see cref="IChatClient"/> surface backed by the ORT-GenAI DirectML runtime via
/// <see cref="HermesOnnxChatModel"/>.
/// </summary>
/// <remarks>
/// The Microsoft.ML.OnnxRuntimeGenAI.DirectML package does not ship a separate
/// Microsoft.Extensions.AI bridge package compatible with this repo's pinned MEAI version;
/// this thin adapter delegates to <see cref="CompletionModelChatClientAdapter"/> so streaming
/// and non-streaming paths stay aligned with other Ouroboros chat models.
/// </remarks>
public sealed class HermesOnnxChatClient : IChatClient
{
    private readonly HermesOnnxChatModel _model;
    private readonly CompletionModelChatClientAdapter _inner;
    private static readonly ChatClientMetadata MetadataValue = new(nameof(HermesOnnxChatClient));

    /// <summary>
    /// Initializes a new instance of the <see cref="HermesOnnxChatClient"/> class.
    /// Initializes a new instance wrapping <paramref name="model"/>.
    /// </summary>
    /// <param name="model">Configured Hermes ONNX chat model. Owned by this client; disposed on <see cref="Dispose"/>.</param>
    public HermesOnnxChatClient(HermesOnnxChatModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _inner = new CompletionModelChatClientAdapter(model);
    }

    /// <inheritdoc/>
    public ChatClientMetadata Metadata => MetadataValue;

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
        {
            return this;
        }

        return _inner.GetService(serviceType, key);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _inner.Dispose();
        _model.Dispose();
    }
}
