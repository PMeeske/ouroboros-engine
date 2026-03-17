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
/// dispatched by <c>FunctionChoiceBehavior.Auto()</c>. Provides observability hooks
/// and a permission gate for dangerous tool invocations.
/// <para>
/// The permission gate uses a delegate-based design so that the engine layer
/// (where this filter lives) does not depend on the application layer.
/// The app layer wires <see cref="DangerousFunctionNames"/> and
/// <see cref="OnPermissionRequired"/> at startup.
/// </para>
/// </summary>
public sealed class OuroborosAutoFunctionFilter : IAutoFunctionInvocationFilter
{
    private readonly ILogger<OuroborosAutoFunctionFilter> _logger;

    /// <summary>
    /// Optional callback invoked after each auto-function call completes.
    /// Parameters: (pluginName, functionName, elapsed, succeeded).
    /// </summary>
    public Action<string, string, TimeSpan, bool>? AfterInvoke { get; set; }

    /// <summary>
    /// When <c>true</c>, functions whose name appears in
    /// <see cref="DangerousFunctionNames"/> will be checked against
    /// <see cref="OnPermissionRequired"/> before execution.
    /// Defaults to <c>false</c> (no gating).
    /// </summary>
    public bool RequireConfirmation { get; set; }

    /// <summary>
    /// Set of function names that are considered dangerous (write/execute operations).
    /// The app layer populates this at startup from its own dangerous-tool list.
    /// Matching is case-insensitive via <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public IReadOnlySet<string> DangerousFunctionNames { get; set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Async callback invoked to request permission before executing a dangerous function.
    /// Parameters: (functionName, arguments). Returns <c>true</c> to allow, <c>false</c> to deny.
    /// When <c>null</c> and <see cref="RequireConfirmation"/> is <c>true</c>, dangerous
    /// function calls are blocked by default (fail-closed).
    /// </summary>
    public Func<string, string, Task<bool>>? OnPermissionRequired { get; set; }

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

        // ── Permission gate for dangerous functions ──────────────────────────
        if (RequireConfirmation && DangerousFunctionNames.Contains(functionName))
        {
            string args = context.Arguments?.ToString() ?? string.Empty;

            if (OnPermissionRequired is not null)
            {
                bool allowed;
                try
                {
                    allowed = await OnPermissionRequired(functionName, args).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // Preceded by OperationCanceledException catch — intentional fallback to FunctionResult error
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogWarning(
                        ex,
                        "Permission check failed for {PluginName}/{FunctionName}",
                        pluginName,
                        functionName);
                    context.Result = new FunctionResult(
                        context.Function,
                        $"Error: Permission check failed for '{functionName}': {ex.Message}");
                    return;
                }

                if (!allowed)
                {
                    _logger.LogInformation(
                        "Permission denied for {PluginName}/{FunctionName}",
                        pluginName,
                        functionName);
                    context.Result = new FunctionResult(
                        context.Function,
                        $"Error: Permission denied for '{functionName}'. User rejected the operation.");
                    return;
                }
            }
            else
            {
                // Fail-closed: no callback registered but confirmation required
                _logger.LogWarning(
                    "Blocked {PluginName}/{FunctionName} — no permission handler registered",
                    pluginName,
                    functionName);
                context.Result = new FunctionResult(
                    context.Function,
                    $"Error: Function '{functionName}' requires permission but no permission handler is registered.");
                return;
            }
        }

        // ── Invoke the function ──────────────────────────────────────────────
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
