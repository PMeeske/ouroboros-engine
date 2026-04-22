// <copyright file="OuroborosOrchestrator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Ouroboros Orchestrator - Self-Improving AI Orchestration
// Implements recursive Plan-Execute-Verify-Learn cycle
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// The Ouroboros Orchestrator is a self-improving AI orchestration system that implements
/// the recursive Plan-Execute-Verify-Learn cycle. It uses the OuroborosAtom for symbolic
/// representation and maintains a self-model that evolves through experience.
///
/// Named after the ancient symbol of a serpent eating its own tail, this orchestrator
/// embodies the principle of recursive self-improvement.
/// </summary>
public sealed partial class OuroborosOrchestrator : OrchestratorBase<string, OuroborosResult>
{
    /// <summary>
    /// Maximum length for output truncation in MeTTa representation.
    /// </summary>
    private const int MeTTaOutputTruncationLength = 100;

    /// <summary>
    /// Quality threshold for capability updates after successful execution.
    /// </summary>
    private const double QualityThresholdForCapabilityUpdate = 0.7;

    /// <summary>
    /// Confidence boost increment when a capability is successfully used.
    /// </summary>
    private const double ConfidenceBoostIncrement = 0.05;

    /// <summary>
    /// Minimum step count for planning decomposition.
    /// </summary>
    private const int MinPlanSteps = 3;

    /// <summary>
    /// Step count range for planning decomposition (added to MinPlanSteps based on granularity).
    /// </summary>
    private const int PlanStepsRange = 7;

    /// <summary>
    /// Base quality threshold for verification (used when strictness is 0.0).
    /// </summary>
    private const double BaseQualityThreshold = 0.3;

    /// <summary>
    /// Quality threshold range multiplier (added to base threshold based on strictness).
    /// </summary>
    private const double QualityThresholdRange = 0.5;

    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISafetyGuard _safety;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly OuroborosAtom _atom;
    private readonly ConcurrentDictionary<string, double> _performanceMetrics = new();
    private readonly Genetic.Core.GeneticAlgorithm<Evolution.PlanStrategyGene>? _strategyEvolver;
    private readonly ILogger<OuroborosOrchestrator> _logger;
    private readonly ToolSelector _toolSelector;
    private readonly Affect.IValenceMonitor? _valenceMonitor;
    private readonly Affect.IPriorityModulator? _priorityModulator;
    private readonly Affect.IUrgeSystem? _urgeSystem;
    private readonly Affect.SpreadingActivation? _spreading;
    private readonly ISkillExtractor? _skillExtractor;
    private readonly ICuriosityEngine? _curiosityEngine;
    private readonly ISkillRegistry? _skillRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosOrchestrator"/> class.
    /// </summary>
    public OuroborosOrchestrator(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISafetyGuard safety,
        IMeTTaEngine mettaEngine,
        OuroborosAtom? atom = null,
        OrchestratorConfig? configuration = null,
        Genetic.Core.GeneticAlgorithm<Evolution.PlanStrategyGene>? strategyEvolver = null,
        ILogger<OuroborosOrchestrator>? logger = null,
        Affect.IValenceMonitor? valenceMonitor = null,
        Affect.IPriorityModulator? priorityModulator = null,
        Affect.IUrgeSystem? urgeSystem = null,
        Affect.SpreadingActivation? spreading = null,
        ISkillExtractor? skillExtractor = null,
        ICuriosityEngine? curiosityEngine = null,
        ISkillRegistry? skillRegistry = null)
        : base("OuroborosOrchestrator", configuration ?? OrchestratorConfig.Default(), safety)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools;
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        ArgumentNullException.ThrowIfNull(safety);
        _safety = safety;
        ArgumentNullException.ThrowIfNull(mettaEngine);
        _mettaEngine = mettaEngine;
        _atom = atom ?? OuroborosAtom.CreateDefault();
        _strategyEvolver = strategyEvolver;
        _logger = logger ?? NullLogger<OuroborosOrchestrator>.Instance;
        _toolSelector = new ToolSelector(_tools.All.ToList(), _llm);
        _valenceMonitor = valenceMonitor;
        _priorityModulator = priorityModulator;
        _urgeSystem = urgeSystem;
        _spreading = spreading;
        _skillExtractor = skillExtractor;
        _curiosityEngine = curiosityEngine;
        _skillRegistry = skillRegistry;
    }

    /// <summary>
    /// Gets the OuroborosAtom representing this orchestrator's self-model.
    /// </summary>
    public OuroborosAtom Atom => _atom;

    /// <summary>
    /// Executes a complete improvement cycle for the given goal.
    /// </summary>
    protected override async Task<OuroborosResult> ExecuteCoreAsync(string goal, OrchestratorContext context)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        List<PhaseResult> phaseResults = new List<PhaseResult>();
        string? finalOutput = null;
        bool success = true;

        try
        {
            // PRE-CYCLE: Affective state update (Psi-theory)
            _urgeSystem?.Tick();
            Affect.AffectiveState? affect = _valenceMonitor?.GetCurrentState();
            Affect.Urge? dominantUrge = _urgeSystem?.GetDominantUrge();

            if (affect != null && dominantUrge != null)
            {
                await ProjectAffectiveStateToMeTTaAsync(affect, dominantUrge, context.CancellationToken).ConfigureAwait(false);
            }

            _atom.SetGoal(goal);
            await TranslateToMeTTaAsync(context.CancellationToken).ConfigureAwait(false);

            // Phase 1: PLAN
            PhaseResult planResult = await ExecutePlanPhaseAsync(goal, context.CancellationToken).ConfigureAwait(false);
            phaseResults.Add(planResult);
            _atom.AdvancePhase();

            if (!planResult.Success)
            {
                success = false;
                return CreateResult(goal, phaseResults, planResult.Output, success, totalStopwatch.Elapsed);
            }

            // Phase 2: EXECUTE
            PhaseResult executeResult = await ExecuteExecutePhaseAsync(planResult.Output, context.CancellationToken).ConfigureAwait(false);
            phaseResults.Add(executeResult);
            _atom.AdvancePhase();

            if (_valenceMonitor != null)
            {
                _valenceMonitor.RecordSignal("execute_phase",
                    executeResult.Success ? 0.5 : -0.5, Affect.SignalType.Valence);
            }

            if (!executeResult.Success)
            {
                success = false;
                return CreateResult(goal, phaseResults, executeResult.Error, success, totalStopwatch.Elapsed);
            }

            // Phase 3: VERIFY
            PhaseResult verifyResult = await ExecuteVerifyPhaseAsync(goal, executeResult.Output, context.CancellationToken).ConfigureAwait(false);
            phaseResults.Add(verifyResult);
            _atom.AdvancePhase();

            if (verifyResult.Success)
            {
                double qualityScore = verifyResult.Metadata.TryGetValue("quality_score", out var qs) ? (double)qs : 0.7;
                _urgeSystem?.Satisfy("certainty", qualityScore);
                _valenceMonitor?.UpdateConfidence("verify", true, qualityScore);
            }
            else
            {
                _valenceMonitor?.UpdateConfidence("verify", false, 0.8);
                _valenceMonitor?.RecordSignal("verify_failed", 0.6, Affect.SignalType.Stress);
            }

            // Phase 4: LEARN
            PhaseResult learnResult = await ExecuteLearnPhaseAsync(goal, phaseResults, context.CancellationToken).ConfigureAwait(false);
            phaseResults.Add(learnResult);
            _atom.AdvancePhase();

            if (learnResult.Success)
            {
                double learnQuality = learnResult.Metadata.TryGetValue("quality_score", out var lqs) ? (double)lqs : 0.5;
                _urgeSystem?.Satisfy("competence", learnQuality);

                if (_atom.AssessConfidence(goal) == OuroborosConfidence.Low)
                {
                    _urgeSystem?.Satisfy("curiosity", 0.8);
                    _valenceMonitor?.UpdateCuriosity(0.7, goal);
                }
            }

            // POST-CYCLE: Update self-model with affective state
            if (_valenceMonitor != null)
            {
                Affect.AffectiveState postCycleAffect = _valenceMonitor.GetCurrentState();
                _atom.UpdateSelfModel("affect_valence", postCycleAffect.Valence);
                _atom.UpdateSelfModel("affect_stress", postCycleAffect.Stress);
                _atom.UpdateSelfModel("affect_arousal", postCycleAffect.Arousal);
                _atom.UpdateSelfModel("dominant_urge", dominantUrge?.Name ?? "none");
            }

            _spreading?.Decay();

            finalOutput = executeResult.Output;
            success = verifyResult.Success;

            totalStopwatch.Stop();
            return CreateResult(goal, phaseResults, finalOutput, success, totalStopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            totalStopwatch.Stop();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            totalStopwatch.Stop();
            return CreateResult(goal, phaseResults, ex.Message, false, totalStopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Translates current Ouroboros state to MeTTa atoms.
    /// </summary>
    private async Task TranslateToMeTTaAsync(CancellationToken ct)
    {
        string metta = _atom.ToMeTTa();
        await _mettaEngine.AddFactAsync(metta, ct).ConfigureAwait(false);
    }

    private OuroborosResult CreateResult(string goal, List<PhaseResult> phases, string? output, bool success, TimeSpan duration)
    {
        return new OuroborosResult(
            Goal: goal,
            Success: success,
            Output: output ?? string.Empty,
            PhaseResults: phases,
            CycleCount: _atom.CycleCount,
            CurrentPhase: _atom.CurrentPhase,
            SelfReflection: _atom.SelfReflect(),
            Duration: duration,
            Metadata: new Dictionary<string, object>
            {
                ["atom_id"] = _atom.InstanceId,
                ["capabilities_count"] = _atom.Capabilities.Count,
                ["experiences_count"] = _atom.Experiences.Count,
            });
    }

    private void RecordPhaseMetric(string phase, double latencyMs, bool success)
    {
        string key = $"{phase}_{(success ? "success" : "failure")}";
        _performanceMetrics.AddOrUpdate(key, 1, (_, count) => count + 1);
        _performanceMetrics.AddOrUpdate($"{phase}_avg_latency", latencyMs, (_, avg) => (avg + latencyMs) / 2);
    }

    private static string EscapeMeTTa(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    /// <inheritdoc/>
    protected override async Task<Dictionary<string, object>> GetCustomHealthAsync(CancellationToken ct)
    {
        Dictionary<string, object> health = await base.GetCustomHealthAsync(ct).ConfigureAwait(false);

        health["ouroboros_atom_id"] = _atom.InstanceId;
        health["current_phase"] = _atom.CurrentPhase.ToString();
        health["cycle_count"] = _atom.CycleCount;
        health["capabilities"] = _atom.Capabilities.Count;
        health["experiences"] = _atom.Experiences.Count;
        health["success_rate"] = _atom.Experiences.Count > 0
            ? _atom.Experiences.Count(e => e.Success) / (double)_atom.Experiences.Count
            : 0.0;

        return health;
    }
}
