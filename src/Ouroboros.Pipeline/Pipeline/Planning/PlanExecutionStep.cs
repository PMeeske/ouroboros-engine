namespace Ouroboros.Pipeline.Planning;

/// <summary>
/// Represents a step in plan execution.
/// </summary>
public class PlanExecutionStep
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// Gets or sets the input to the step.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Gets or sets the output from the step.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Gets or sets whether the step succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets the duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
}