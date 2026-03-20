// <copyright file="SmcpPatternMatcher.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;
using Ouroboros.Tools.MeTTa.Smcp;

namespace Ouroboros.Pipeline.Smcp;

/// <summary>
/// Watches the AtomSpace for <c>MkIntent</c> atoms and evaluates them against
/// registered tool activation patterns. When a tool's pattern matches an intent
/// with sufficient confidence, a <c>ToolActivation</c> atom is added to the space.
/// <para>
/// This is the heart of SMCP: tools self-select by declaring patterns that match
/// the LLM's semantic intent, rather than being explicitly invoked.
/// </para>
/// </summary>
public sealed class SmcpPatternMatcher : IDisposable
{
    private readonly HyperonMeTTaEngine _engine;
    private readonly SmcpConfidenceScorer _scorer;
    private readonly SmcpConflictResolver _resolver;
    private readonly SmcpConfidenceConfig _config;
    private readonly List<SmcpToolAdapter> _adapters = new();
    private readonly object _adapterLock = new();
    private bool _subscribed;
    private bool _disposed;

    /// <summary>
    /// Raised when a tool activation is produced (for downstream executor consumption).
    /// </summary>
    public event Action<SmcpToolMatch>? ToolActivated;

    /// <summary>
    /// Raised when a clarification is needed (confidence in the ambiguous zone).
    /// </summary>
    public event Action<Expression>? ClarificationNeeded;

    /// <summary>
    /// Gets the number of registered tool patterns.
    /// </summary>
    public int RegisteredPatternCount
    {
        get { lock (_adapterLock) return _adapters.Count; }
    }

    /// <summary>
    /// Initializes a new SMCP pattern matcher.
    /// </summary>
    /// <param name="engine">The MeTTa engine whose AtomSpace is monitored.</param>
    /// <param name="scorer">Confidence scorer for computing composite scores.</param>
    /// <param name="resolver">Conflict resolver for multi-match scenarios.</param>
    /// <param name="config">Confidence gating configuration.</param>
    public SmcpPatternMatcher(
        HyperonMeTTaEngine engine,
        SmcpConfidenceScorer? scorer = null,
        SmcpConflictResolver? resolver = null,
        SmcpConfidenceConfig? config = null)
    {
        _engine = engine;
        _scorer = scorer ?? new SmcpConfidenceScorer();
        _resolver = resolver ?? new SmcpConflictResolver();
        _config = config ?? SmcpConfidenceConfig.Default;
    }

    /// <summary>
    /// Registers a tool adapter for pattern matching.
    /// </summary>
    public void RegisterAdapter(SmcpToolAdapter adapter)
    {
        lock (_adapterLock)
            _adapters.Add(adapter);
    }

    /// <summary>
    /// Registers multiple adapters (typically from <see cref="SmcpToolRegistrar.RegisterAll"/>).
    /// </summary>
    public void RegisterAdapters(IEnumerable<SmcpToolAdapter> adapters)
    {
        lock (_adapterLock)
            _adapters.AddRange(adapters);
    }

    /// <summary>
    /// Subscribes to <see cref="HyperonMeTTaEngine.AtomAdded"/> and begins watching
    /// for <c>MkIntent</c> atoms. Call once after all tools are registered.
    /// </summary>
    public void StartWatching()
    {
        if (_subscribed) return;
        _engine.AtomAdded += OnAtomAdded;
        _subscribed = true;
    }

    /// <summary>
    /// Stops watching for new atoms.
    /// </summary>
    public void StopWatching()
    {
        if (!_subscribed) return;
        _engine.AtomAdded -= OnAtomAdded;
        _subscribed = false;
    }

    /// <summary>
    /// Evaluates an intent atom against all registered tool patterns.
    /// Can be called directly (pull mode) instead of relying on AtomAdded events (push mode).
    /// </summary>
    /// <param name="intentAtom">An <c>MkIntent</c> expression atom.</param>
    /// <returns>The resolved tool matches that should fire.</returns>
    public IReadOnlyList<SmcpToolMatch> EvaluateIntent(Expression intentAtom)
    {
        List<SmcpToolAdapter> snapshot;
        lock (_adapterLock)
            snapshot = new List<SmcpToolAdapter>(_adapters);

        var matches = new List<SmcpToolMatch>();

        foreach (var adapter in snapshot)
        {
            // 1. Structural unification: does the intent match the tool's pattern?
            var sub = Unifier.Unify(adapter.ActivationPattern.Pattern, intentAtom);
            if (sub is null) continue;

            // 2. Keyword guard: do the intent's args contain relevant keywords?
            if (adapter.ActivationPattern.Keywords.Count > 0)
            {
                var intentArgs = ExtractIntentArgs(intentAtom);
                if (!adapter.MatchesKeywords(intentArgs))
                    continue;
            }

            // 3. Confidence scoring
            double composite = _scorer.Score(intentAtom, adapter, sub);
            var decision = _config.Gate(composite);

            switch (decision)
            {
                case SmcpGateDecision.Fire:
                    matches.Add(new SmcpToolMatch(
                        adapter.Tool.Name, intentAtom, sub, composite));
                    break;

                case SmcpGateDecision.Clarify:
                    var clarification = SmcpAtomFactory.ClarificationRequest(
                        "auto",
                        $"Intent matched {adapter.Tool.Name} with confidence {composite:F2} — clarification needed",
                        intentAtom);
                    _engine.AddAtom(clarification);
                    ClarificationNeeded?.Invoke(clarification);
                    break;

                    // Ignore and Reject: do nothing
            }
        }

        // 4. Conflict resolution
        var resolved = _resolver.Resolve(matches);

        // 5. Reify activations into the AtomSpace
        foreach (var match in resolved)
        {
            var activation = SmcpAtomFactory.ToolActivation(
                match.ToolName, match.MatchedIntent, match.CompositeConfidence);
            _engine.AddAtom(activation);

            var confidence = SmcpAtomFactory.CompositeConfidence(
                match.ToolName, match.MatchedIntent, match.CompositeConfidence);
            _engine.AddAtom(confidence);

            ToolActivated?.Invoke(match);
        }

        return resolved;
    }

    /// <summary>
    /// Handles the AtomAdded event — only processes MkIntent atoms.
    /// </summary>
    private void OnAtomAdded(Atom atom)
    {
        if (atom is not Expression expr) return;
        if (expr.Children.Count < 1) return;
        if (expr.Children[0] is not Symbol head || head.Name != "MkIntent") return;

        EvaluateIntent(expr);
    }

    /// <summary>
    /// Extracts argument strings from an MkIntent atom's args child.
    /// </summary>
    private static IEnumerable<string> ExtractIntentArgs(Expression intentAtom)
    {
        if (intentAtom.Children.Count < 3) yield break;

        var argsAtom = intentAtom.Children[2];
        if (argsAtom is Expression argsExpr)
        {
            foreach (var child in argsExpr.Children)
                yield return child.ToSExpr().Trim('"');
        }
        else
        {
            yield return argsAtom.ToSExpr().Trim('"');
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
    }
}
