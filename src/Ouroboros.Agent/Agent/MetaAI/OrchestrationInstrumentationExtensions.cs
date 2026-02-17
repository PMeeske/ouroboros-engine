namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Extension methods for instrumenting orchestration components.
/// </summary>
public static class OrchestrationInstrumentationExtensions
{
    /// <summary>
    /// Wraps an async operation with tracing.
    /// </summary>
    public static async Task<Result<T, string>> WithTracing<T>(
        this Task<Result<T, string>> operation,
        string operationName,
        Dictionary<string, object?>? tags = null)
    {
        using var activity = OrchestrationTracing.ActivitySource.StartActivity(
            operationName,
            ActivityKind.Internal,
            default(ActivityContext),
            tags!);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation;
            stopwatch.Stop();

            result.Match(
                success =>
                {
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.SetTag("orchestrator.success", true);
                    return success;
                },
                error =>
                {
                    activity?.SetStatus(ActivityStatusCode.Error, error);
                    activity?.SetTag("orchestrator.success", false);
                    activity?.SetTag("orchestrator.error", error);
                    return default!;
                });

            activity?.SetTag("orchestrator.duration_ms", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            OrchestrationTracing.RecordError(activity, operationName, ex);
            throw;
        }
    }
}