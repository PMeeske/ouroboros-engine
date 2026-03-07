// <copyright file="OuroborosAutoFunctionFilter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Ouroboros.SemanticKernel.Filters;

/// <summary>
/// Semantic Kernel auto-function invocation filter that logs every function call
/// dispatched by <c>FunctionChoiceBehavior.Auto()</c>. Provides an extensible
/// hook point for future event-bus publishing, metrics collection, and permission gates.
/// </summary>
public sealed class OuroborosAutoFunctionFilter : IAutoFunctionInvocationFilter
{
    private readonly ILogger<OuroborosAutoFunctionFilter> _logger;

    /// <summary>
    /// Optional callback invoked after each auto-function call completes.
    /// Parameters: (pluginName, functionName, elapsed, succeeded).
    /// </summary>
    public Action<string, string, TimeSpan, bool>? AfterInvoke { get; set; }

    public OuroborosAutoFunctionFilter(ILogger<OuroborosAutoFunctionFilter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "S2139:Exceptions should be either logged or rethrown but not both",
        Justification = "Intentionally log for observability and rethrow to let SK handle the failure")]
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        string pluginName = context.Function.PluginName ?? "(none)";
        string functionName = context.Function.Name;

        _logger.LogInformation(
            "SK AutoFunction invoking {PluginName}/{FunctionName} (iteration {Iteration})",
            pluginName,
            functionName,
            context.RequestSequenceIndex);

        var sw = Stopwatch.StartNew();
        bool succeeded = false;

        try
        {
            await next(context).ConfigureAwait(false);
            succeeded = true;

            _logger.LogInformation(
                "SK AutoFunction {PluginName}/{FunctionName} completed in {ElapsedMs:F1}ms",
                pluginName,
                functionName,
                sw.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SK AutoFunction {PluginName}/{FunctionName} failed after {ElapsedMs:F1}ms",
                pluginName,
                functionName,
                sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            sw.Stop();
            AfterInvoke?.Invoke(pluginName, functionName, sw.Elapsed, succeeded);
        }
    }
}
