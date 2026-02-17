namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the severity level of a cognitive event or alert.
/// </summary>
public enum Severity
{
    /// <summary>
    /// Informational event - normal cognitive processing.
    /// </summary>
    Info,

    /// <summary>
    /// Warning event - requires attention but not critical.
    /// </summary>
    Warning,

    /// <summary>
    /// Critical event - requires immediate attention.
    /// </summary>
    Critical,
}