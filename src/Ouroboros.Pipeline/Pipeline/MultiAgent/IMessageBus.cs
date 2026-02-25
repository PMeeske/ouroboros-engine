namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the contract for a message bus enabling inter-agent communication.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the message bus asynchronously.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(AgentMessage message, CancellationToken ct = default);

    /// <summary>
    /// Sends a request message and awaits a response with the specified timeout.
    /// </summary>
    /// <param name="request">The request message to send.</param>
    /// <param name="timeout">The maximum time to wait for a response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task containing the response message.</returns>
    Task<AgentMessage> RequestAsync(AgentMessage request, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Subscribes an agent to receive messages matching the specified topic filter.
    /// </summary>
    /// <param name="agentId">The unique identifier of the subscribing agent.</param>
    /// <param name="topicFilter">The topic filter, or null to receive all topics.</param>
    /// <param name="handler">The async handler function for processing received messages.</param>
    /// <returns>The subscription for managing the subscription lifecycle.</returns>
    Subscription Subscribe(Guid agentId, string? topicFilter, Func<AgentMessage, Task> handler);

    /// <summary>
    /// Unsubscribes a subscription from the message bus.
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription to remove.</param>
    void Unsubscribe(Guid subscriptionId);

    /// <summary>
    /// Gets all pending messages for a specific agent.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <returns>A read-only list of pending messages.</returns>
    IReadOnlyList<AgentMessage> GetPendingMessages(Guid agentId);
}