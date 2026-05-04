// <copyright file="Phi3VisionOnnxChatClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.Extensions.AI;
using Ouroboros.Providers.Phi3Vision;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// MEAI <see cref="IChatClient"/> surface for Phi-3.5-vision-instruct ONNX
/// (DirectML INT4). Inspects each <see cref="ChatMessage"/> for image content
/// (<see cref="DataContent"/>) and feeds the first image to the underlying
/// <see cref="Phi3VisionOnnxChatModel"/>'s multimodal pipeline. Falls back to
/// pure text when no image is supplied.
/// </summary>
/// <remarks>
/// <para>
/// Phi-3.5-vision supports multiple images per turn (<c>&lt;|image_1|&gt;</c>..<c>&lt;|image_n|&gt;</c>).
/// v1 of this adapter only routes the first image — that covers Iaret's
/// avatar perception use cases (mirror recognition, frame review,
/// expression classification) which are all single-image. Multi-image is a
/// clean follow-up.
/// </para>
/// <para>
/// Streaming is not implemented in v1 — generation is synchronous and
/// returns a single <see cref="ChatResponse"/>. <see cref="GetStreamingResponseAsync"/>
/// emits one update with the final text. Adding streaming requires
/// integrating <see cref="Phi3VisionOnnxChatModel"/>'s token loop into an
/// <see cref="IAsyncEnumerable{T}"/>; deferred to follow-up phase.
/// </para>
/// </remarks>
public sealed class Phi3VisionOnnxChatClient : IChatClient
{
    private readonly Phi3VisionOnnxChatModel _model;
    private static readonly ChatClientMetadata MetadataValue = new(nameof(Phi3VisionOnnxChatClient));

    /// <summary>
    /// Initializes a new instance of the <see cref="Phi3VisionOnnxChatClient"/> class.
    /// </summary>
    /// <param name="model">Configured Phi3v chat model. Owned by this client; disposed on <see cref="Dispose"/>.</param>
    public Phi3VisionOnnxChatClient(Phi3VisionOnnxChatModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
    }

    /// <inheritdoc/>
    public ChatClientMetadata Metadata => MetadataValue;

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        (string prompt, byte[]? imageBytes) = ExtractPromptAndFirstImage(messages);
        string text = await _model.GenerateAsync(prompt, imageBytes, cancellationToken).ConfigureAwait(false);

        ChatMessage reply = new(ChatRole.Assistant, text);
        return new ChatResponse(reply);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse final = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        foreach (ChatMessage msg in final.Messages)
        {
            yield return new ChatResponseUpdate(msg.Role, msg.Text);
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? key = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (key is null && serviceType == typeof(IChatClient))
        {
            return this;
        }

        return null;
    }

    /// <summary>
    /// Walks <paramref name="messages"/> and assembles a single text prompt while
    /// extracting the first image payload. System and user messages are merged
    /// into a flat instruction; assistant messages are included as prior turns.
    /// </summary>
    private static (string Prompt, byte[]? ImageBytes) ExtractPromptAndFirstImage(IEnumerable<ChatMessage> messages)
    {
        StringBuilder sb = new();
        byte[]? firstImage = null;

        foreach (ChatMessage msg in messages)
        {
            string role = msg.Role.Value;
            string text = msg.Text ?? string.Empty;

            if (firstImage is null)
            {
                foreach (AIContent content in msg.Contents)
                {
                    if (content is DataContent dc && IsImageMediaType(dc.MediaType))
                    {
                        firstImage = dc.Data.ToArray();
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(role).Append(": ").Append(text);
            }
        }

        return (sb.ToString(), firstImage);
    }

    private static bool IsImageMediaType(string? mediaType) =>
        !string.IsNullOrEmpty(mediaType) && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Dispose()
    {
        _model.Dispose();
    }
}
