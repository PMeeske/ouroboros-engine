// ==========================================================
// Meta-AI Planner Orchestrator Implementation
// Implements plan-execute-verify loop with continual learning
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of the Meta-AI v2 planner/executor/verifier orchestrator.
/// Coordinates planning, execution, verification, and learning in a continuous loop.
/// </summary>
public sealed partial class MetaAIPlannerOrchestrator : IMetaAIPlannerOrchestrator
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly IUncertaintyRouter _router;
    private readonly ISafetyGuard _safety;
    private readonly IEthicsFramework _ethics;
    private readonly IHumanApprovalProvider _approvalProvider;
    private readonly ISkillExtractor? _skillExtractor;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();

    public MetaAIPlannerOrchestrator(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISkillRegistry skills,
        IUncertaintyRouter router,
        ISafetyGuard safety,
        IEthicsFramework ethics,
        IHumanApprovalProvider? approvalProvider = null,
        ISkillExtractor? skillExtractor = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _ = _router;
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _ethics = ethics ?? throw new ArgumentNullException(nameof(ethics));
        _approvalProvider = approvalProvider ?? new AutoDenyApprovalProvider();
        _skillExtractor = skillExtractor ?? new SkillExtractor(llm, skills, ethics);
    }

    /// <summary>
    /// Learns from execution experience to improve future planning.
    /// </summary>
    public void LearnFromExecution(PlanVerificationResult verification)
    {
        if (verification == null)
            return;

                // Store experience in memory using factory
        Experience experience = ExperienceFactory.FromExecution(
            goal: verification.Execution.Plan.Goal,
            execution: verification.Execution,
            verification: verification,
            metadata: new Dictionary<string, object>
            {
                ["quality_score"] = verification.QualityScore,
                ["verified"] = verification.Verified
            });

        _ = _memory.StoreExperienceAsync(experience);

        // If execution was successful and high quality, extract a skill
        if (verification.Verified && verification.QualityScore > 0.8 && _skillExtractor != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    bool shouldExtract = await _skillExtractor.ShouldExtractSkillAsync(verification);
                    if (shouldExtract)
                    {
                        Result<Skill, string> skillResult = await _skillExtractor.ExtractSkillAsync(
                            verification.Execution,
                            verification);

                        skillResult.Match(
                            skill =>
                            {
                                RecordMetric("skill_extraction_success", 1.0, true);
                                Trace.TraceInformation("Extracted skill: {0} (Quality: {1})", skill.Name, skill.SuccessRate.ToString("P0"));
                            },
                            error =>
                            {
                                RecordMetric("skill_extraction_failure", 1.0, false);
                                Trace.TraceWarning("Skill extraction failed: {0}", error);
                            });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    RecordMetric("skill_extraction_error", 1.0, false);
                    Trace.TraceWarning("Skill extraction error: {0}", ex.Message);
                }
            });
        }

        RecordMetric("learning", 1.0, true);
    }

    /// <summary>
    /// Gets performance metrics for the orchestrator.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        => new Dictionary<string, PerformanceMetrics>(_metrics);

    private void RecordMetric(string component, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            component,
            key => new PerformanceMetrics(
                key,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            (key, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newAvgLatency = ((existing.AverageLatencyMs * existing.ExecutionCount) + latencyMs) / newCount;
                double newSuccessRate = ((existing.SuccessRate * existing.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;

                return new PerformanceMetrics(
                    key,
                    ExecutionCount: newCount,
                    AverageLatencyMs: newAvgLatency,
                    SuccessRate: newSuccessRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: existing.CustomMetrics);
            });
    }
}
