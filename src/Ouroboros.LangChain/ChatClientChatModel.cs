// <copyright file="ChatClientChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Runtime.CompilerServices;
using LangChain.Providers;
using Microsoft.Extensions.AI;
using Ouroboros.Abstractions.Core;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using LcMessage = LangChain.Providers.Message;
using LcMessageRole = LangChain.Providers.MessageRole;
using LcChatRequest = LangChain.Providers.ChatRequest;
using LcChatResponse = LangChain.Providers.ChatResponse;

namespace Ouroboros.LangChainBridge;

/// <summary>
/// Wraps an MEAI <see cref="IChatClient"/> (or <see cref="IOuroborosChatClient"/>)
/// as a LangChain <see cref="ChatModel"/>, enabling Ouroboros providers to participate
/// in LangChain chains, agents, and pipelines.
/// </summary>
public class ChatClientChatModel : ChatModel
{
    private readonly IChatClient _client;

    /// <summary>
    /// Initializes a new instance wrapping the given <see cref="IChatClient"/>.
    /// </summary>
    /// <param name="client">The MEAI chat client to delegate to.</param>
    public ChatClientChatModel(IChatClient client)
        : base("ouroboros-meai")
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>
    /// Initializes a new instance from an <see cref="IOuroborosChatClient"/>
    /// (which is already an <see cref="IChatClient"/>).
    /// </summary>
    public ChatClientChatModel(IOuroborosChatClient client)
        : this((IChatClient)client)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom model identifier.
    /// </summary>
    public ChatClientChatModel(IChatClient client, string modelId)
        : base(modelId)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public override int ContextLength => 128_000; // sensible default; override per-model as needed

    /// <inheritdoc />
    public override async IAsyncEnumerable<LcChatResponse> GenerateAsync(
        LcChatRequest request,
        ChatSettings? settings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Convert LangChain messages → MEAI ChatMessage list
        var meaiMessages = new List<ChatMessage>();
        if (request.Messages != null)
        {
            foreach (var msg in request.Messages)
            {
                meaiMessages.Add(ToMeaiMessage(msg));
            }
        }

        // Call MEAI IChatClient
        var response = await _client.GetResponseAsync(
            meaiMessages,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Convert MEAI response → LangChain ChatResponse
        string text = response.Text ?? string.Empty;

        var responseMessages = new List<LcMessage>
        {
            new(text, LcMessageRole.Ai)
        };

        yield return new LcChatResponse
        {
            Messages = responseMessages,
            UsedSettings = settings ?? new ChatSettings(),
        };
    }

    /// <summary>
    /// Converts a LangChain <see cref="LcMessage"/> to an MEAI <see cref="ChatMessage"/>.
    /// </summary>
    private static ChatMessage ToMeaiMessage(LcMessage msg)
    {
        ChatRole role = msg.Role switch
        {
            LcMessageRole.System => ChatRole.System,
            LcMessageRole.Ai => ChatRole.Assistant,
            LcMessageRole.ToolCall => ChatRole.Tool,
            LcMessageRole.ToolResult => ChatRole.Tool,
            _ => ChatRole.User, // Human, Chat, and others → User
        };

        return new ChatMessage(role, msg.Content ?? string.Empty);
    }
}
