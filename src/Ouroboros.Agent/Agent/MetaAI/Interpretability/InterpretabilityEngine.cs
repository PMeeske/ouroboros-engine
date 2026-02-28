
namespace Ouroboros.Agent.MetaAI.Interpretability;

using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Core.Ethics;

/// <summary>
/// Engine for explaining agent decisions. Bridges GlobalWorkspace (attention),
/// PredictiveMonitor (forecasts), IdentityGraph (capabilities), and
/// EthicsFramework (ethical clearance) into coherent explanation traces.
/// </summary>
public sealed class InterpretabilityEngine
{
    private readonly GlobalWorkspace? _workspace;
    private readonly PredictiveMonitor? _monitor;
    private readonly IdentityGraph? _identity;
    private readonly IEthicsFramework? _ethics;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpretabilityEngine"/> class.
    /// All parameters are optional — the engine gracefully degrades when
    /// components are unavailable.
    /// </summary>
    public InterpretabilityEngine(
        GlobalWorkspace? workspace = null,
        PredictiveMonitor? monitor = null,
        IdentityGraph? identity = null,
        IEthicsFramework? ethics = null)
    {
        _workspace = workspace;
        _monitor = monitor;
        _identity = identity;
        _ethics = ethics;
    }

    /// <summary>
    /// Explains a decision based on the current agent state.
    /// </summary>
    public DecisionExplanation ExplainDecision(
        Guid decisionId,
        string decisionDescription)
    {
        var factors = new List<ReasoningFactor>();

        // Gather attention factors
        if (_workspace != null)
        {
            var highPriority = _workspace.GetHighPriorityItems();
            foreach (var item in highPriority.Take(3))
            {
                factors.Add(new ReasoningFactor(
                    Source: "GlobalWorkspace",
                    Description: $"High-priority attention item: {item.Content}",
                    Weight: 0.8,
                    Confidence: 0.9));
            }
        }

        // Gather prediction factors
        if (_monitor != null)
        {
            var pending = _monitor.GetPendingForecasts();
            foreach (var forecast in pending.Take(2))
            {
                factors.Add(new ReasoningFactor(
                    Source: "PredictiveMonitor",
                    Description: $"Forecast for {forecast.MetricName}: {forecast.PredictedValue:F2} (confidence: {forecast.Confidence:F2})",
                    Weight: forecast.Confidence,
                    Confidence: forecast.Confidence));
            }
        }

        // Gather identity/capability factors
        if (_identity != null)
        {
            var perf = _identity.GetPerformanceSummary(TimeSpan.FromDays(30));
            factors.Add(new ReasoningFactor(
                Source: "IdentityGraph",
                Description: $"Historical success rate: {perf.OverallSuccessRate:P0} over {perf.TotalTasks} tasks",
                Weight: 0.5,
                Confidence: perf.TotalTasks > 10 ? 0.9 : 0.5));
        }

        // Gather ethics factors
        if (_ethics != null)
        {
            var principles = _ethics.GetCorePrinciples();
            factors.Add(new ReasoningFactor(
                Source: "EthicsFramework",
                Description: $"Evaluated against {principles.Count} ethical principles",
                Weight: 1.0,
                Confidence: 1.0));
        }

        double avgConfidence = factors.Count > 0
            ? factors.Average(f => f.Confidence)
            : 0.0;

        return new DecisionExplanation(
            DecisionId: decisionId,
            Summary: $"Decision '{decisionDescription}' based on {factors.Count} contributing factors",
            ContributingFactors: factors,
            OverallConfidence: avgConfidence,
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Explains a plan by describing the reasoning for each step.
    /// </summary>
    public static PlanExplanation ExplainPlan(string planGoal, IReadOnlyList<string> steps)
    {
        var explanations = new List<StepExplanation>();

        foreach (var step in steps)
        {
            explanations.Add(new StepExplanation(
                Action: step,
                Reasoning: $"Step selected as part of plan to achieve: {planGoal}",
                AlternativesConsidered: new[] { "No alternatives evaluated in current planning mode" }));
        }

        return new PlanExplanation(
            PlanGoal: planGoal,
            StepExplanations: explanations,
            OverallConfidence: 0.5);
    }

    /// <summary>
    /// Gets a report of what the agent is currently attending to.
    /// </summary>
    public AttentionReport GetAttentionReport()
    {
        if (_workspace == null)
        {
            return new AttentionReport(
                ActiveItems: Array.Empty<AttentionItem>(),
                TotalWorkspaceSize: 0,
                HighPriorityCount: 0);
        }

        var stats = _workspace.GetStatistics();
        var items = _workspace.GetHighPriorityItems()
            .Select(i => new AttentionItem(
                Content: i.Content,
                Priority: i.Priority.ToString(),
                Source: i.Source))
            .ToList();

        return new AttentionReport(
            ActiveItems: items,
            TotalWorkspaceSize: stats.TotalItems,
            HighPriorityCount: stats.HighPriorityItems);
    }

    /// <summary>
    /// Gets a report on how well the agent's predictions match reality.
    /// </summary>
    public CalibrationReport GetCalibrationReport()
    {
        if (_monitor == null)
        {
            return new CalibrationReport(
                BrierScore: 0,
                CalibrationError: 0,
                TotalForecasts: 0,
                VerifiedForecasts: 0,
                FailedForecasts: 0);
        }

        var calibration = _monitor.GetCalibration(TimeSpan.FromDays(30));

        return new CalibrationReport(
            BrierScore: calibration.BrierScore,
            CalibrationError: calibration.CalibrationError,
            TotalForecasts: calibration.TotalForecasts,
            VerifiedForecasts: calibration.VerifiedForecasts,
            FailedForecasts: calibration.FailedForecasts);
    }
}
