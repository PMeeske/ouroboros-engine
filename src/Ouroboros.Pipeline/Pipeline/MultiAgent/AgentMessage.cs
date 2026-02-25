// <copyright file="MessageBus.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a communication unit between agents in a multi-agent system.
/// </summary>
/// <param name="Id">The unique identifier of the message.</param>
/// <param name="SenderId">The unique identifier of the sending agent.</param>
/// <param name="ReceiverId">The unique identifier of the receiving agent, or null for broadcasts.</param>
/// <param name="Type">The type of message.</param>
/// <param name="Priority">The priority level of the message.</param>
/// <param name="Topic">The topic or channel of the message.</param>
/// <param name="Payload">The message payload data.</param>
/// <param name="Timestamp">The timestamp when the message was created.</param>
/// <param name="CorrelationId">The correlation identifier linking requests and responses.</param>
public sealed record AgentMessage(
    Guid Id,
    Guid SenderId,
    Guid? ReceiverId,
    MessageType Type,
    MessagePriority Priority,
    string Topic,
    object Payload,
    DateTime Timestamp,
    Guid? CorrelationId)
{
    /// <summary>
    /// Gets a value indicating whether this message is a request expecting a response.
    /// </summary>
    public bool IsRequest => Type == MessageType.Request;

    /// <summary>
    /// Gets a value indicating whether this message is a broadcast to all subscribers.
    /// </summary>
    public bool IsBroadcast => Type == MessageType.Broadcast || ReceiverId is null;

    /// <summary>
    /// Creates a request message expecting a response from a specific agent.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="receiverId">The unique identifier of the receiving agent.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <returns>A new request message.</returns>
    public static AgentMessage CreateRequest(Guid senderId, Guid receiverId, string topic, object payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        Guid messageId = Guid.NewGuid();
        return new AgentMessage(
            Id: messageId,
            SenderId: senderId,
            ReceiverId: receiverId,
            Type: MessageType.Request,
            Priority: MessagePriority.Normal,
            Topic: topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: messageId);
    }

    /// <summary>
    /// Creates a response message to a previous request.
    /// </summary>
    /// <param name="request">The original request message being responded to.</param>
    /// <param name="payload">The response payload data.</param>
    /// <returns>A new response message correlated to the original request.</returns>
    public static AgentMessage CreateResponse(AgentMessage request, object payload)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(payload);

        if (request.Type != MessageType.Request)
        {
            throw new ArgumentException("Can only create response for request messages.", nameof(request));
        }

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: request.ReceiverId ?? throw new ArgumentException("Request must have a receiver.", nameof(request)),
            ReceiverId: request.SenderId,
            Type: MessageType.Response,
            Priority: request.Priority,
            Topic: request.Topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: request.CorrelationId);
    }

    /// <summary>
    /// Creates a broadcast message sent to all subscribers of a topic.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <returns>A new broadcast message.</returns>
    public static AgentMessage CreateBroadcast(Guid senderId, string topic, object payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: senderId,
            ReceiverId: null,
            Type: MessageType.Broadcast,
            Priority: MessagePriority.Normal,
            Topic: topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: null);
    }

    /// <summary>
    /// Creates a notification message that does not expect a response.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <param name="receiverId">The unique identifier of the receiving agent, or null for broadcast notifications.</param>
    /// <returns>A new notification message.</returns>
    public static AgentMessage CreateNotification(Guid senderId, string topic, object payload, Guid? receiverId = null)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: senderId,
            ReceiverId: receiverId,
            Type: MessageType.Notification,
            Priority: MessagePriority.Normal,
            Topic: topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: null);
    }

    /// <summary>
    /// Creates an error message indicating a failure.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="receiverId">The unique identifier of the receiving agent, or null for broadcasts.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A new error message.</returns>
    public static AgentMessage CreateError(Guid senderId, Guid? receiverId, string topic, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: senderId,
            ReceiverId: receiverId,
            Type: MessageType.Error,
            Priority: MessagePriority.High,
            Topic: topic,
            Payload: errorMessage,
            Timestamp: DateTime.UtcNow,
            CorrelationId: null);
    }
}