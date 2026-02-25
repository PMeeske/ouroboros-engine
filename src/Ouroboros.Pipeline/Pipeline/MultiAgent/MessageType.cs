namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the type of agent message.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// A request message expecting a response.
    /// </summary>
    Request,

    /// <summary>
    /// A response to a previous request.
    /// </summary>
    Response,

    /// <summary>
    /// A broadcast message sent to all subscribers.
    /// </summary>
    Broadcast,

    /// <summary>
    /// A notification message that does not expect a response.
    /// </summary>
    Notification,

    /// <summary>
    /// An error message indicating a failure.
    /// </summary>
    Error,
}