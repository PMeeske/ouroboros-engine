// <copyright file="SmcpExecutor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Ouroboros.Core.Hyperon;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;
using Ouroboros.Tools.MeTTa.Smcp;

namespace Ouroboros.Pipeline.Smcp;

/// <summary>
/// Executes tool activations produced by the <see cref="SmcpPatternMatcher"/>.
/// Watches for <c>ToolActivation</c> atoms in the AtomSpace, invokes the corresponding
/// <see cref="ITool"/>, and reifies results back as <c>MkToolResult</c> atoms.
/// <para>
/// Supports both event-driven (AtomAdded subscription) and direct invocation modes.
/// </para>
/// </summary>
public sealed class SmcpExecutor : IDisposable
{
    private readonly HyperonMeTTaEngine _engine;
    private readonly ToolRegistry _toolRegistry;
    private readonly SmcpConfidenceScorer _scorer;
    private readonly ConcurrentDictionary<string, byte> _executedActivations = new();
    private bool _subscribed;
    private bool _disposed;

    /// <summary>
    /// Raised after a tool has been successfully executed, with the result atom.
    /// </summary>
    public event Action<Expression>? ResultAvailable;

    /// <summary>
    /// Raised when a tool execution fails.
    /// </summary>
    public event Action<Expression>? ErrorOccurred;

    /// <summary>
    /// Initializes a new SMCP executor.
    /// </summary>
    /// <param name="engine">The MeTTa engine for AtomSpace access.</param>
    /// <param name="toolRegistry">Registry containing the actual tool implementations.</param>
    /// <param name="scorer">Confidence scorer for recording execution outcomes.</param>
    public SmcpExecutor(
        HyperonMeTTaEngine engine,
        ToolRegistry toolRegistry,
        SmcpConfidenceScorer? scorer = null)
    {
        _engine = engine;
        _toolRegistry = toolRegistry;
        _scorer = scorer ?? new SmcpConfidenceScorer();
    }

    /// <summary>
    /// Subscribes to <see cref="HyperonMeTTaEngine.AtomAdded"/> and begins watching
    /// for <c>ToolActivation</c> atoms.
    /// </summary>
    public void StartWatching()
    {
        if (_subscribed) return;
        _engine.AtomAdded += OnAtomAdded;
        _subscribed = true;
    }

    /// <summary>
    /// Stops watching for activation atoms.
    /// </summary>
    public void StopWatching()
    {
        if (!_subscribed) return;
        _engine.AtomAdded -= OnAtomAdded;
        _subscribed = false;
    }

    /// <summary>
    /// Directly executes a tool match (from <see cref="SmcpPatternMatcher.EvaluateIntent"/>).
    /// Invokes the tool, records the outcome, and reifies the result into the AtomSpace.
    /// </summary>
    /// <param name="match">The tool match to execute.</param>
    /// <param name="correlationId">Correlation ID for result provenance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result atom (MkToolResult or MkToolError).</returns>
    public async Task<Expression> ExecuteAsync(
        SmcpToolMatch match, string correlationId, CancellationToken ct = default)
    {
        var tool = _toolRegistry.Get(match.ToolName);
        if (tool is null)
        {
            var error = SmcpAtomFactory.MkToolError(
                match.ToolName, $"Tool '{match.ToolName}' not found in registry", correlationId);
            _engine.AddAtom(error);
            ErrorOccurred?.Invoke(error);
            return error;
        }

        // Build input from intent args
        var input = BuildToolInput(match.MatchedIntent);

        try
        {
            var result = await tool.InvokeAsync(input, ct).ConfigureAwait(false);

            return result.Match(
                success =>
                {
                    _scorer.RecordOutcome(match.ToolName, true);
                    var resultAtom = SmcpAtomFactory.MkToolResult(
                        match.ToolName, success, match.CompositeConfidence, correlationId);
                    _engine.AddAtom(resultAtom);
                    ResultAvailable?.Invoke(resultAtom);
                    return resultAtom;
                },
                error =>
                {
                    _scorer.RecordOutcome(match.ToolName, false);
                    var errorAtom = SmcpAtomFactory.MkToolError(
                        match.ToolName, error, correlationId);
                    _engine.AddAtom(errorAtom);
                    ErrorOccurred?.Invoke(errorAtom);
                    return errorAtom;
                });
        }
#pragma warning disable CA1031 // Tool execution must not crash the SMCP pipeline
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _scorer.RecordOutcome(match.ToolName, false);
            var errorAtom = SmcpAtomFactory.MkToolError(
                match.ToolName, ex.Message, correlationId);
            _engine.AddAtom(errorAtom);
            ErrorOccurred?.Invoke(errorAtom);
            return errorAtom;
        }
    }

    /// <summary>
    /// Executes all tool matches from a pattern evaluation, potentially in parallel
    /// for non-overlapping tools.
    /// </summary>
    /// <param name="matches">Resolved tool matches.</param>
    /// <param name="correlationId">Correlation ID for provenance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All result atoms.</returns>
    public async Task<IReadOnlyList<Expression>> ExecuteAllAsync(
        IReadOnlyList<SmcpToolMatch> matches, string correlationId, CancellationToken ct = default)
    {
        if (matches.Count == 0)
            return Array.Empty<Expression>();

        if (matches.Count == 1)
            return new[] { await ExecuteAsync(matches[0], correlationId, ct).ConfigureAwait(false) };

        // Parallel execution for multiple non-overlapping matches
        var tasks = matches.Select(m => ExecuteAsync(m, correlationId, ct));
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles ToolActivation atoms added to the AtomSpace.
    /// </summary>
    private void OnAtomAdded(Atom atom)
    {
        if (atom is not Expression expr) return;
        if (expr.Children.Count < 4) return;
        if (expr.Children[0] is not Symbol head || head.Name != "ToolActivation") return;

        // Deduplicate — don't execute the same activation twice
        var key = expr.ToSExpr();
        if (!_executedActivations.TryAdd(key, 0)) return;

        var toolName = expr.Children[1].ToSExpr().Trim('"');
        var intentAtom = expr.Children[2] as Expression;
        if (intentAtom is null) return;

        if (!double.TryParse(expr.Children[3].ToSExpr(), out double confidence))
            return;

        var match = new SmcpToolMatch(
            toolName, intentAtom, Substitution.Empty, confidence);

        // Fire-and-forget — results are reified via events
        _ = ExecuteAsync(match, $"smcp-{DateTime.UtcNow:HHmmss}").ContinueWith(t =>
        {
            if (t.Exception != null)
                System.Diagnostics.Debug.WriteLine(
                    $"[SMCP] Executor error for {toolName}: {t.Exception.Flatten().Message}");
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Builds a tool input string from an MkIntent atom's arguments.
    /// </summary>
    private static string BuildToolInput(Expression intentAtom)
    {
        if (intentAtom.Children.Count < 3) return string.Empty;

        var argsAtom = intentAtom.Children[2];
        if (argsAtom is Expression argsExpr)
        {
            // Join args as JSON-like input
            var args = argsExpr.Children.Select(c => c.ToSExpr().Trim('"'));
            return string.Join(" ", args);
        }

        return argsAtom.ToSExpr().Trim('"');
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
    }
}
