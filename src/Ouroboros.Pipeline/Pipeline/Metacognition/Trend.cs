namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the trend direction of a performance metric over time.
/// </summary>
public enum Trend
{
    /// <summary>
    /// Performance is getting better over time.
    /// </summary>
    Improving,

    /// <summary>
    /// Performance is remaining constant.
    /// </summary>
    Stable,

    /// <summary>
    /// Performance is getting worse over time.
    /// </summary>
    Declining,

    /// <summary>
    /// Performance shows high variance with no clear direction.
    /// </summary>
    Volatile,

    /// <summary>
    /// Insufficient data to determine trend.
    /// </summary>
    Unknown,
}