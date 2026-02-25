namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Disposable scope for tracing orchestration operations.
/// </summary>
public sealed class OrchestrationScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly Stopwatch _stopwatch;
    private readonly Action<Activity?, TimeSpan> _onComplete;
    private bool _completed;

    private OrchestrationScope(Activity? activity, Action<Activity?, TimeSpan> onComplete)
    {
        _activity = activity;
        _stopwatch = Stopwatch.StartNew();
        _onComplete = onComplete;
    }

    /// <summary>
    /// Creates a model selection scope.
    /// </summary>
    public static OrchestrationScope ModelSelection(string prompt, string? contextInfo = null)
    {
        var activity = OrchestrationTracing.StartModelSelection(prompt, contextInfo);
        return new OrchestrationScope(activity, (a, d) =>
        {
            // Default completion - actual completion should be called explicitly
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Creates a routing scope.
    /// </summary>
    public static OrchestrationScope Routing(string task)
    {
        var activity = OrchestrationTracing.StartRouting(task);
        return new OrchestrationScope(activity, (a, d) =>
        {
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Creates a plan creation scope.
    /// </summary>
    public static OrchestrationScope PlanCreation(string goal, int maxDepth)
    {
        var activity = OrchestrationTracing.StartPlanCreation(goal, maxDepth);
        return new OrchestrationScope(activity, (a, d) =>
        {
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Creates a plan execution scope.
    /// </summary>
    public static OrchestrationScope PlanExecution(Guid planId, int stepCount)
    {
        var activity = OrchestrationTracing.StartPlanExecution(planId, stepCount);
        return new OrchestrationScope(activity, (a, d) =>
        {
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Gets the elapsed time.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Gets the activity.
    /// </summary>
    public Activity? Activity => _activity;

    /// <summary>
    /// Marks the operation as successful with model selection details.
    /// </summary>
    public void CompleteModelSelection(string selectedModel, UseCaseType useCase, double confidenceScore)
    {
        _completed = true;
        _stopwatch.Stop();
        OrchestrationTracing.CompleteModelSelection(_activity, selectedModel, useCase, confidenceScore, _stopwatch.Elapsed);
    }

    /// <summary>
    /// Marks the operation as failed.
    /// </summary>
    public void Fail(string error)
    {
        _completed = true;
        _stopwatch.Stop();
        _activity?.SetStatus(ActivityStatusCode.Error, error);
    }

    /// <summary>
    /// Records an exception.
    /// </summary>
    public void RecordException(Exception exception)
    {
        OrchestrationTracing.RecordError(_activity, _activity?.OperationName ?? "unknown", exception);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _stopwatch.Stop();
            _onComplete(_activity, _stopwatch.Elapsed);
        }

        _activity?.Dispose();
    }
}