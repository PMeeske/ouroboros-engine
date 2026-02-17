namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Interface for online learning algorithms that process streaming feedback.
/// </summary>
public interface IOnlineLearner
{
    /// <summary>
    /// Gets the current performance metrics.
    /// </summary>
    OnlineLearningMetrics Metrics { get; }

    /// <summary>
    /// Processes a single feedback item and computes updates.
    /// </summary>
    /// <param name="feedback">The feedback to process.</param>
    /// <returns>A Result containing the computed updates or an error.</returns>
    Result<IReadOnlyList<LearningUpdate>, string> ProcessFeedback(Feedback feedback);

    /// <summary>
    /// Processes a batch of feedback items.
    /// </summary>
    /// <param name="batch">The batch of feedback to process.</param>
    /// <returns>A Result containing the aggregated updates or an error.</returns>
    Result<IReadOnlyList<LearningUpdate>, string> ProcessBatch(IEnumerable<Feedback> batch);

    /// <summary>
    /// Gets all pending updates that have not yet been applied.
    /// </summary>
    /// <returns>The list of pending updates.</returns>
    IReadOnlyList<LearningUpdate> GetPendingUpdates();

    /// <summary>
    /// Applies all accumulated updates to the internal parameters.
    /// </summary>
    /// <returns>A Result indicating success or failure.</returns>
    Result<int, string> ApplyUpdates();

    /// <summary>
    /// Gets the current value of a parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>An Option containing the parameter value if it exists.</returns>
    Option<double> GetParameter(string parameterName);

    /// <summary>
    /// Sets a parameter value directly.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    void SetParameter(string parameterName, double value);
}