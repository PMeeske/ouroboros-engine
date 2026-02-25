namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the priority level for agent messages.
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// Low priority messages that can be processed when resources are available.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority messages for standard communication.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority messages that should be processed before normal messages.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority messages that require immediate processing.
    /// </summary>
    Critical,
}