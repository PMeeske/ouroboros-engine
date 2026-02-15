#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Uncertainty Router Implementation
// Routes based on confidence with fallback strategies
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of uncertainty-aware routing with fallback strategies.
/// </summary>
public sealed class UncertaintyRouter : IUncertaintyRouter
{
    private readonly IModelOrchestrator _orchestrator;
    private readonly double _minConfidenceThreshold;
    private readonly double _humanOversightThreshold;
    private readonly ConcurrentDictionary<string, List<(string context, double confidence, bool success)>> _routingHistory = new();

    public UncertaintyRouter(
        IModelOrchestrator orchestrator,
        double minConfidenceThreshold = 0.7,
        double humanOversightThreshold = 0.5)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _minConfidenceThreshold = Math.Clamp(minConfidenceThreshold, 0.0, 1.0);
        _humanOversightThreshold = Math.Clamp(humanOversightThreshold, 0.0, 1.0);
    }

    /// <inheritdoc/>
    public async Task<RoutingDecision> RouteDecisionAsync(
        string context,
        string proposedAction,
        double confidenceLevel,
        CancellationToken ct = default)
    {
        using var activity = OrchestrationTracing.StartRouting(proposedAction);
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(proposedAction))
        {
            stopwatch.Stop();
            OrchestrationTracing.CompleteRouting(activity, "error", 0, false, stopwatch.Elapsed, success: false);
            return new RoutingDecision(
                ShouldProceed: false,
                ConfidenceLevel: 0.0,
                RecommendedStrategy: FallbackStrategy.Abort,
                Reason: "Proposed action cannot be empty",
                RequiresHumanOversight: false,
                AlternativeActions: Array.Empty<string>());
        }

        // Determine if we should proceed based on confidence
        bool shouldProceed = confidenceLevel >= _minConfidenceThreshold;
        
        // Get fallback strategy
        FallbackStrategy strategy = await GetFallbackStrategyAsync(confidenceLevel, 0, ct);
        
        // Determine if human oversight is needed
        double riskLevel = CalculateRiskLevel(context, proposedAction);
        bool requiresOversight = await RequiresHumanOversightAsync(context, riskLevel, confidenceLevel, ct);

        // Generate alternative actions if confidence is low
        List<string> alternatives = new();
        if (!shouldProceed || requiresOversight)
        {
            alternatives = GenerateAlternatives(proposedAction, confidenceLevel, strategy);
        }

        string reason = BuildReason(confidenceLevel, strategy, requiresOversight);

        stopwatch.Stop();
        OrchestrationTracing.CompleteRouting(
            activity,
            strategy.ToString(),
            confidenceLevel,
            !shouldProceed,
            stopwatch.Elapsed,
            success: shouldProceed);

        // Record in history
        RecordRoutingOutcome(context, confidenceLevel, shouldProceed);

        return new RoutingDecision(
            ShouldProceed: shouldProceed,
            ConfidenceLevel: confidenceLevel,
            RecommendedStrategy: strategy,
            Reason: reason,
            RequiresHumanOversight: requiresOversight,
            AlternativeActions: alternatives);
    }

    /// <inheritdoc/>
    public async Task<bool> RequiresHumanOversightAsync(
        string context,
        double riskLevel,
        double confidenceLevel,
        CancellationToken ct = default)
    {
        // Require human oversight if:
        // 1. Risk is high and confidence is low
        // 2. Confidence is below human oversight threshold
        // 3. Context suggests critical decision
        
        if (confidenceLevel < _humanOversightThreshold)
            return true;

        if (riskLevel > 0.7 && confidenceLevel < 0.8)
            return true;

        // Check if context indicates critical situation
        bool isCritical = context.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
                         context.Contains("important", StringComparison.OrdinalIgnoreCase) ||
                         context.Contains("sensitive", StringComparison.OrdinalIgnoreCase);

        if (isCritical && confidenceLevel < 0.9)
            return true;

        return await Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<FallbackStrategy> GetFallbackStrategyAsync(
        double confidenceLevel,
        int attemptCount,
        CancellationToken ct = default)
    {
        // If we've attempted multiple times, escalate or abort
        if (attemptCount > 3)
        {
            return confidenceLevel < 0.3 ? FallbackStrategy.Abort : FallbackStrategy.EscalateToHuman;
        }

        // Very low confidence - need more information or abort
        if (confidenceLevel < 0.3)
        {
            return attemptCount == 0 ? FallbackStrategy.RequestClarification : FallbackStrategy.Abort;
        }

        // Low confidence - try conservative approach or retry
        if (confidenceLevel < 0.5)
        {
            return attemptCount == 0 ? FallbackStrategy.UseConservativeApproach : FallbackStrategy.Retry;
        }

        // Moderate confidence - retry or defer
        if (confidenceLevel < _minConfidenceThreshold)
        {
            return attemptCount == 0 ? FallbackStrategy.Retry : FallbackStrategy.Defer;
        }

        // High confidence - conservative approach as safety net
        return await Task.FromResult(FallbackStrategy.UseConservativeApproach);
    }

    private double CalculateRiskLevel(string context, string proposedAction)
    {
        double risk = 0.5; // Base risk level

        // Increase risk for certain keywords
        string[] highRiskKeywords = { "delete", "remove", "drop", "terminate", "destroy", "critical" };
        string[] moderateRiskKeywords = { "modify", "update", "change", "alter" };

        string lowerAction = proposedAction.ToLowerInvariant();
        string lowerContext = context.ToLowerInvariant();

        if (highRiskKeywords.Any(k => lowerAction.Contains(k) || lowerContext.Contains(k)))
            risk += 0.3;

        if (moderateRiskKeywords.Any(k => lowerAction.Contains(k) || lowerContext.Contains(k)))
            risk += 0.15;

        return Math.Clamp(risk, 0.0, 1.0);
    }

    private List<string> GenerateAlternatives(string proposedAction, double confidence, FallbackStrategy strategy)
    {
        List<string> alternatives = new();

        switch (strategy)
        {
            case FallbackStrategy.Retry:
                alternatives.Add("Retry with more context");
                alternatives.Add("Break down into smaller steps");
                break;

            case FallbackStrategy.EscalateToHuman:
                alternatives.Add("Request human review and approval");
                alternatives.Add("Defer decision to human operator");
                break;

            case FallbackStrategy.UseConservativeApproach:
                alternatives.Add("Use read-only or safer alternative");
                alternatives.Add("Apply minimal changes");
                break;

            case FallbackStrategy.Defer:
                alternatives.Add("Postpone until more information is available");
                alternatives.Add("Gather additional context first");
                break;

            case FallbackStrategy.RequestClarification:
                alternatives.Add("Request more specific instructions");
                alternatives.Add("Ask for examples or clarification");
                break;

            case FallbackStrategy.Abort:
                alternatives.Add("Cancel operation");
                alternatives.Add("Return to previous state");
                break;
        }

        return alternatives;
    }

    private string BuildReason(double confidence, FallbackStrategy strategy, bool requiresOversight)
    {
        if (confidence >= _minConfidenceThreshold && !requiresOversight)
        {
            return $"High confidence ({confidence:P0}), proceeding with action";
        }

        if (requiresOversight)
        {
            return $"Confidence {confidence:P0} requires human oversight, strategy: {strategy}";
        }

        return $"Low confidence ({confidence:P0}), recommended strategy: {strategy}";
    }

    private void RecordRoutingOutcome(string context, double confidence, bool success)
    {
        string key = context.Length > 50 ? context[..Math.Min(50, context.Length)] : context;
        
        _routingHistory.AddOrUpdate(
            key,
            _ => new List<(string, double, bool)> { (context, confidence, success) },
            (_, existing) =>
            {
                existing.Add((context, confidence, success));
                // Keep only recent history (last 100 entries)
                return existing.TakeLast(100).ToList();
            });
    }
}
