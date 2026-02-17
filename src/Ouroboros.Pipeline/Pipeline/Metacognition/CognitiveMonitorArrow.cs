using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Provides Kleisli arrows for cognitive monitoring operations in the pipeline.
/// Enables functional composition of monitoring with other pipeline operations.
/// </summary>
public static class CognitiveMonitorArrow
{
    /// <summary>
    /// Creates a Kleisli arrow that records a cognitive event.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from CognitiveEvent to Unit.</returns>
    public static KleisliResult<CognitiveEvent, Unit, string> RecordArrow(ICognitiveMonitor monitor)
        => cognitiveEvent => Task.FromResult(monitor.RecordEvent(cognitiveEvent));

    /// <summary>
    /// Creates a Kleisli arrow that gets the current cognitive health.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from Unit to CognitiveHealth.</returns>
    public static KleisliResult<Unit, CognitiveHealth, string> HealthCheckArrow(ICognitiveMonitor monitor)
        => _ => Task.FromResult(Result<CognitiveHealth, string>.Success(monitor.GetHealth()));

    /// <summary>
    /// Creates a Kleisli arrow that gets recent events.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from count to list of events.</returns>
    public static KleisliResult<int, ImmutableList<CognitiveEvent>, string> GetRecentEventsArrow(ICognitiveMonitor monitor)
        => count =>
        {
            if (count < 0)
            {
                return Task.FromResult(Result<ImmutableList<CognitiveEvent>, string>.Failure("Count must be non-negative."));
            }

            return Task.FromResult(Result<ImmutableList<CognitiveEvent>, string>.Success(monitor.GetRecentEvents(count)));
        };

    /// <summary>
    /// Creates a Kleisli arrow that acknowledges an alert.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from alert ID to Unit.</returns>
    public static KleisliResult<Guid, Unit, string> AcknowledgeAlertArrow(ICognitiveMonitor monitor)
        => alertId => Task.FromResult(monitor.AcknowledgeAlert(alertId));

    /// <summary>
    /// Creates a Kleisli arrow that sets a monitoring threshold.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from (metric, threshold) to Unit.</returns>
    public static KleisliResult<(string Metric, double Threshold), Unit, string> SetThresholdArrow(ICognitiveMonitor monitor)
        => input => Task.FromResult(monitor.SetThreshold(input.Metric, input.Threshold));

    /// <summary>
    /// Creates a Kleisli arrow that records an event and returns the health status.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from CognitiveEvent to CognitiveHealth.</returns>
    public static KleisliResult<CognitiveEvent, CognitiveHealth, string> RecordAndCheckHealthArrow(ICognitiveMonitor monitor)
        => async cognitiveEvent =>
        {
            var recordResult = monitor.RecordEvent(cognitiveEvent);
            if (recordResult.IsFailure)
            {
                return Result<CognitiveHealth, string>.Failure(recordResult.Error);
            }

            return Result<CognitiveHealth, string>.Success(monitor.GetHealth());
        };

    /// <summary>
    /// Creates a Kleisli arrow that monitors an operation and records the appropriate cognitive event.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <param name="operation">The operation to monitor.</param>
    /// <param name="eventDescription">Description for the cognitive event.</param>
    /// <returns>A Kleisli arrow that wraps the operation with monitoring.</returns>
    public static KleisliResult<TInput, TOutput, string> MonitoredOperationArrow<TInput, TOutput>(
        ICognitiveMonitor monitor,
        Func<TInput, Task<Result<TOutput, string>>> operation,
        string eventDescription)
        => async input =>
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var result = await operation(input);
                var duration = DateTime.UtcNow - startTime;

                if (result.IsSuccess)
                {
                    var successEvent = CognitiveEvent.Decision(
                        $"{eventDescription}: Completed successfully",
                        ImmutableDictionary<string, object>.Empty
                            .Add("latency_ms", duration.TotalMilliseconds)
                            .Add("input_type", typeof(TInput).Name)
                            .Add("output_type", typeof(TOutput).Name));

                    monitor.RecordEvent(successEvent);
                }
                else
                {
                    var errorEvent = CognitiveEvent.Error(
                        $"{eventDescription}: Failed - {result.Error}",
                        Severity.Warning,
                        ImmutableDictionary<string, object>.Empty
                            .Add("latency_ms", duration.TotalMilliseconds)
                            .Add("error", result.Error));

                    monitor.RecordEvent(errorEvent);
                }

                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                var exceptionEvent = CognitiveEvent.Error(
                    $"{eventDescription}: Exception - {ex.Message}",
                    Severity.Critical,
                    ImmutableDictionary<string, object>.Empty
                        .Add("latency_ms", duration.TotalMilliseconds)
                        .Add("exception_type", ex.GetType().Name)
                        .Add("exception_message", ex.Message));

                monitor.RecordEvent(exceptionEvent);

                return Result<TOutput, string>.Failure($"Operation failed with exception: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a Kleisli arrow that checks health and fails if critical.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from Unit to CognitiveHealth that fails if critical.</returns>
    public static KleisliResult<Unit, CognitiveHealth, string> HealthGateArrow(ICognitiveMonitor monitor)
        => _ =>
        {
            var health = monitor.GetHealth();

            if (health.IsCritical())
            {
                return Task.FromResult(Result<CognitiveHealth, string>.Failure(
                    $"Cognitive health is critical (score: {health.HealthScore:P1}). Processing halted."));
            }

            return Task.FromResult(Result<CognitiveHealth, string>.Success(health));
        };
}