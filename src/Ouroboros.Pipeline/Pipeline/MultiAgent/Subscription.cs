namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a subscription for receiving messages from the message bus.
/// </summary>
/// <param name="Id">The unique identifier of the subscription.</param>
/// <param name="AgentId">The unique identifier of the subscribing agent.</param>
/// <param name="TopicFilter">The topic filter, or null to receive all topics.</param>
/// <param name="Handler">The async handler function for processing received messages.</param>
public sealed record Subscription(
    Guid Id,
    Guid AgentId,
    string? TopicFilter,
    Func<AgentMessage, Task> Handler)
{
    /// <summary>
    /// Determines whether the subscription matches the given message.
    /// </summary>
    /// <param name="message">The message to check.</param>
    /// <returns>True if the subscription matches the message; otherwise, false.</returns>
    public bool Matches(AgentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Check if message is targeted at this agent or is a broadcast
        bool isTargeted = message.ReceiverId == AgentId || message.IsBroadcast;

        // Check topic filter
        bool topicMatches = TopicFilter is null ||
                            string.Equals(TopicFilter, message.Topic, StringComparison.OrdinalIgnoreCase);

        return isTargeted && topicMatches;
    }
}