namespace Ouroboros.Pipeline.Reasoning;

/// <summary>
/// A trace entry from reasoning.
/// </summary>
public class ReasoningTraceEntry
{
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public required string Event { get; set; }

    /// <summary>
    /// Gets or sets the step name.
    /// </summary>
    public required string StepName { get; set; }

    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}