namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the overall health status of cognitive processing.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// All cognitive processes are functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// Some degradation detected but still functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Significant impairment affecting cognitive function.
    /// </summary>
    Impaired,

    /// <summary>
    /// Critical state requiring immediate intervention.
    /// </summary>
    Critical,
}