using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Provides extension methods for working with the message bus.
/// </summary>
public static class MessageBusExtensions
{
    /// <summary>
    /// Publishes a request and returns the response wrapped in a Result monad.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="request">The request message.</param>
    /// <param name="timeout">The maximum time to wait for a response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A Result containing the response message or an error.</returns>
    public static async Task<Result<AgentMessage>> TryRequestAsync(
        this IMessageBus bus,
        AgentMessage request,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AgentMessage response = await bus.RequestAsync(request, timeout, ct).ConfigureAwait(false);
            return Result<AgentMessage>.Success(response);
        }
        catch (TimeoutException ex)
        {
            return Result<AgentMessage>.Failure($"Request timed out: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return Result<AgentMessage>.Failure("Request was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<AgentMessage>.Failure($"Request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Publishes a message and returns a Result indicating success or failure.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A Result indicating success or containing an error.</returns>
    public static async Task<Result<Unit>> TryPublishAsync(
        this IMessageBus bus,
        AgentMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await bus.PublishAsync(message, ct).ConfigureAwait(false);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit>.Failure("Publish was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure($"Publish failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Subscribes to messages and wraps the handler result in an Option monad.
    /// </summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <param name="bus">The message bus.</param>
    /// <param name="agentId">The unique identifier of the subscribing agent.</param>
    /// <param name="topicFilter">The topic filter, or null to receive all topics.</param>
    /// <param name="handler">The handler function that receives the typed payload.</param>
    /// <returns>The subscription for managing the subscription lifecycle.</returns>
    public static Subscription SubscribeTyped<T>(
        this IMessageBus bus,
        Guid agentId,
        string? topicFilter,
        Func<AgentMessage, Option<T>, Task> handler)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);

        return bus.Subscribe(agentId, topicFilter, async message =>
        {
            Option<T> typedPayload = message.Payload is T payload
                ? Option<T>.Some(payload)
                : Option<T>.None();

            await handler(message, typedPayload).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Creates a broadcast message and publishes it.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic of the broadcast.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task BroadcastAsync(
        this IMessageBus bus,
        Guid senderId,
        string topic,
        object payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        AgentMessage broadcast = AgentMessage.CreateBroadcast(senderId, topic, payload);
        await bus.PublishAsync(broadcast, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a notification message and publishes it.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic of the notification.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="receiverId">The unique identifier of the receiving agent, or null for broadcast notifications.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task NotifyAsync(
        this IMessageBus bus,
        Guid senderId,
        string topic,
        object payload,
        Guid? receiverId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        AgentMessage notification = AgentMessage.CreateNotification(senderId, topic, payload, receiverId);
        await bus.PublishAsync(notification, ct).ConfigureAwait(false);
    }
}