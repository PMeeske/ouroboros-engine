// <copyright file="ChatClientCompletionModelAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Wraps a MEAI <see cref="IChatClient"/> as an Ouroboros <see cref="IChatCompletionModel"/>.
/// Enables any MEAI-compatible client (Azure OpenAI, Google AI, etc.) to be used
/// as an Ouroboros provider without writing a custom adapter.
/// </summary>
public sealed class ChatClientCompletionModelAdapter : IChatCompletionModel
{
    private readonly IChatClient _client;

    public ChatClientCompletionModelAdapter(IChatClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Gets the underlying <see cref="IChatClient"/> for direct MEAI access.
    /// </summary>
    public IChatClient Client => _client;

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt),
        };

        ChatResponse response = await _client.GetResponseAsync(messages, cancellationToken: ct)
            .ConfigureAwait(false);

        return response.Text ?? string.Empty;
    }
}
