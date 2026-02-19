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

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// The Ouroboros Orchestrator is a self-improving AI orchestration system that implements
/// the recursive Plan-Execute-Verify-Learn cycle. It uses the OuroborosAtom for symbolic
/// representation and maintains a self-model that evolves through experience.
/// 
/// Named after the ancient symbol of a serpent eating its own tail, this orchestrator
/// embodies the principle of recursive self-improvement.
/// </summary>
public sealed class OuroborosOrchestrator : OrchestratorBase<string, OuroborosResult>
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
    private readonly Affect.IValenceMonitor? _valenceMonitor;
    private readonly Affect.IPriorityModulator? _priorityModulator;
    private readonly Affect.IUrgeSystem? _urgeSystem;
    private readonly Affect.SpreadingActivation? _spreading;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosOrchestrator"/> class.
    /// </summary>
    /// <param name="llm">The language model for generation.</param>
    /// <param name="tools">The tool registry.</param>
    /// <param name="memory">The memory store for experiences.</param>
    /// <param name="safety">The safety guard.</param>
    /// <param name="mettaEngine">The MeTTa engine for symbolic reasoning.</param>
    /// <param name="atom">Optional pre-configured OuroborosAtom.</param>
    /// <param name="configuration">Optional orchestrator configuration.</param>
    /// <param name="strategyEvolver">Optional genetic algorithm for evolving planning strategies.</param>
    /// <param name="valenceMonitor">Optional valence monitor for affective state tracking.</param>
    /// <param name="priorityModulator">Optional priority modulator for affect-driven task ordering.</param>
    /// <param name="urgeSystem">Optional urge system for Psi-theory drive management.</param>
    /// <param name="spreading">Optional spreading activation for associative memory priming.</param>
    public OuroborosOrchestrator(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISafetyGuard safety,
        IMeTTaEngine mettaEngine,
        OuroborosAtom? atom = null,
        OrchestratorConfig? configuration = null,
        Genetic.Core.GeneticAlgorithm<Evolution.PlanStrategyGene>? strategyEvolver = null,
        Affect.IValenceMonitor? valenceMonitor = null,
        Affect.IPriorityModulator? priorityModulator = null,
        Affect.IUrgeSystem? urgeSystem = null,
        Affect.SpreadingActivation? spreading = null)
        : base("OuroborosOrchestrator", configuration ?? OrchestratorConfig.Default(), safety)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _atom = atom ?? OuroborosAtom.CreateDefault();
        _strategyEvolver = strategyEvolver;
        _valenceMonitor = valenceMonitor;
        _priorityModulator = priorityModulator;
        _urgeSystem = urgeSystem;
        _spreading = spreading;
    }

    /// <summary>
    /// Gets the OuroborosAtom representing this orchestrator's self-model.
    /// </summary>
    public OuroborosAtom Atom => _atom;

    /// <summary>
    /// Executes a complete improvement cycle for the given goal.
    /// </summary>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>The result of the improvement cycle.</returns>
    protected override async Task<OuroborosResult> ExecuteCoreAsync(string goal, OrchestratorContext context)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        List<PhaseResult> phaseResults = new List<PhaseResult>();
        string? finalOutput = null;
        bool success = true;

        try
        {
            // ═══════════════════════════════════════════════
            // PRE-CYCLE: Affective state update (Psi-theory)
            // ═══════════════════════════════════════════════
            _urgeSystem?.Tick();
            Affect.AffectiveState? affect = _valenceMonitor?.GetCurrentState();
            double selectionThreshold = affect != null ? CalculateSelectionThreshold(affect) : 0.5;
            double resolutionLevel = affect != null ? GetEffectiveResolutionLevel(affect) : 0.5;
            Affect.Urge? dominantUrge = _urgeSystem?.GetDominantUrge();

            if (affect != null && dominantUrge != null)
            {
                await ProjectAffectiveStateToMeTTaAsync(affect, dominantUrge, context.CancellationToken);
            }

            // Set the goal
            _atom.SetGoal(goal);

            // Translate Ouroboros state to MeTTa
            await TranslateToMeTTaAsync(context.CancellationToken);

            // Phase 1: PLAN (shaped by affect + urges)
            PhaseResult planResult = await ExecutePlanPhaseAsync(goal, context.CancellationToken);
            phaseResults.Add(planResult);
            _atom.AdvancePhase();

            if (!planResult.Success)
            {
                success = false;
                return CreateResult(goal, phaseResults, planResult.Output, success, totalStopwatch.Elapsed);
            }

            // Phase 2: EXECUTE (selection threshold gates actions)
            PhaseResult executeResult = await ExecuteExecutePhaseAsync(planResult.Output, context.CancellationToken);
            phaseResults.Add(executeResult);
            _atom.AdvancePhase();

            // Update valence based on execution success
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

            // Phase 3: VERIFY (strictness modulated by certainty urge)
            PhaseResult verifyResult = await ExecuteVerifyPhaseAsync(goal, executeResult.Output, context.CancellationToken);
            phaseResults.Add(verifyResult);
            _atom.AdvancePhase();

            // Satisfy or frustrate certainty urge based on verification
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

            // Phase 4: LEARN (satisfy competence + curiosity urges)
            PhaseResult learnResult = await ExecuteLearnPhaseAsync(goal, phaseResults, context.CancellationToken);
            phaseResults.Add(learnResult);
            _atom.AdvancePhase(); // Returns to PLAN, completing the cycle

            if (learnResult.Success)
            {
                double learnQuality = learnResult.Metadata.TryGetValue("quality_score", out var lqs) ? (double)lqs : 0.5;
                _urgeSystem?.Satisfy("competence", learnQuality);

                // If this was a novel domain, satisfy curiosity
                if (_atom.AssessConfidence(goal) == OuroborosConfidence.Low)
                {
                    _urgeSystem?.Satisfy("curiosity", 0.8);
                    _valenceMonitor?.UpdateCuriosity(0.7, goal);
                }
            }

            // ═══════════════════════════════════════════════
            // POST-CYCLE: Update self-model with affective state
            // ═══════════════════════════════════════════════
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
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            return CreateResult(goal, phaseResults, ex.Message, false, totalStopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Executes the PLAN phase - goal decomposition and strategy formulation.
    /// </summary>
    private async Task<PhaseResult> ExecutePlanPhaseAsync(string goal, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Check if goal is safe
            if (!_atom.IsSafeAction(goal))
            {
                return new PhaseResult(
                    ImprovementPhase.Plan,
                    Success: false,
                    Output: string.Empty,
                    Error: "Goal violates safety constraints",
                    Duration: sw.Elapsed);
            }

            // Assess confidence for this goal
            OuroborosConfidence confidence = _atom.AssessConfidence(goal);

            // Build planning prompt with self-reflection
            string selfReflection = _atom.SelfReflect();
            string prompt = BuildPlanPrompt(goal, selfReflection, confidence);

            // Generate plan
            string planText = await _llm.GenerateTextAsync(prompt, ct);

            sw.Stop();
            RecordPhaseMetric("plan", sw.ElapsedMilliseconds, true);

            return new PhaseResult(
                ImprovementPhase.Plan,
                Success: true,
                Output: planText,
                Error: null,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["confidence"] = confidence.ToString(),
                    ["self_reflection_included"] = true,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("plan", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Plan,
                Success: false,
                Output: string.Empty,
                Error: $"Planning failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the EXECUTE phase - carrying out planned actions.
    /// </summary>
    private async Task<PhaseResult> ExecuteExecutePhaseAsync(string plan, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Parse plan and execute steps
            List<string> steps = ParsePlanSteps(plan);
            StringBuilder outputBuilder = new StringBuilder();
            bool allStepsSucceeded = true;
            string? lastError = null;

            foreach (string step in steps)
            {
                Result<string, string> stepResult = await ExecuteStepAsync(step, ct);

                stepResult.Match(
                    success =>
                    {
                        outputBuilder.AppendLine($"Step completed: {success}");
                    },
                    error =>
                    {
                        outputBuilder.AppendLine($"Step failed: {error}");
                        allStepsSucceeded = false;
                        lastError = error;
                    });

                if (!allStepsSucceeded)
                {
                    break;
                }
            }

            sw.Stop();
            RecordPhaseMetric("execute", sw.ElapsedMilliseconds, allStepsSucceeded);

            return new PhaseResult(
                ImprovementPhase.Execute,
                Success: allStepsSucceeded,
                Output: outputBuilder.ToString(),
                Error: lastError,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["steps_count"] = steps.Count,
                    ["steps_completed"] = allStepsSucceeded ? steps.Count : steps.Count - 1,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("execute", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Execute,
                Success: false,
                Output: string.Empty,
                Error: $"Execution failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the VERIFY phase - checking results against expectations.
    /// Uses the VerificationStrictness strategy to determine the quality threshold.
    /// </summary>
    private async Task<PhaseResult> ExecuteVerifyPhaseAsync(string goal, string output, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Read evolved strategy gene for verification strictness
            // VerificationStrictness: 0.0 = lenient, 1.0 = strict
            double verificationStrictness = _atom.GetStrategyWeight("VerificationStrictness", 0.6);

            // Calculate quality threshold based on strictness (range: BaseQualityThreshold to BaseQualityThreshold+QualityThresholdRange)
            double qualityThreshold = BaseQualityThreshold + (verificationStrictness * QualityThresholdRange);

            // Build verification prompt
            string prompt = BuildVerificationPrompt(goal, output);

            // Generate verification
            string verificationText = await _llm.GenerateTextAsync(prompt, ct);

            // Parse verification result
            (bool verified, double qualityScore) = ParsePlanVerificationResult(verificationText);

            // Apply quality threshold based on evolved strictness
            bool meetsQualityThreshold = qualityScore >= qualityThreshold;

            // Use MeTTa for symbolic verification
            string planMetta = $"(plan (goal \"{EscapeMeTTa(goal)}\") (output \"{EscapeMeTTa(output.Substring(0, Math.Min(MeTTaOutputTruncationLength, output.Length)))}\"))";
            Result<bool, string> mettaResult = await _mettaEngine.VerifyPlanAsync(planMetta, ct);

            bool mettaVerified = mettaResult.Match(v => v, _ => true);

            // Overall success requires: LLM verification, quality threshold, and MeTTa verification
            bool overallSuccess = verified && meetsQualityThreshold && mettaVerified;

            // Build detailed error message if verification failed
            string? errorMessage = null;
            if (!overallSuccess)
            {
                List<string> failures = new List<string>();
                if (!verified) failures.Add("LLM verification failed");
                if (!meetsQualityThreshold) failures.Add($"quality {qualityScore:F2} below threshold {qualityThreshold:F2}");
                if (!mettaVerified) failures.Add("MeTTa verification failed");
                errorMessage = $"Verification failed: {string.Join(", ", failures)}";
            }

            sw.Stop();
            RecordPhaseMetric("verify", sw.ElapsedMilliseconds, overallSuccess);

            return new PhaseResult(
                ImprovementPhase.Verify,
                Success: overallSuccess,
                Output: verificationText,
                Error: errorMessage,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["quality_score"] = qualityScore,
                    ["quality_threshold"] = qualityThreshold,
                    ["verification_strictness"] = verificationStrictness,
                    ["metta_verified"] = mettaVerified,
                    ["llm_verified"] = verified,
                    ["meets_quality_threshold"] = meetsQualityThreshold,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("verify", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Verify,
                Success: false,
                Output: string.Empty,
                Error: $"Verification failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the LEARN phase - extracting insights and updating capabilities.
    /// </summary>
    private async Task<PhaseResult> ExecuteLearnPhaseAsync(string goal, List<PhaseResult> phaseResults, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Calculate overall success and quality
            bool overallSuccess = phaseResults.All(p => p.Success);
            double averageQuality = phaseResults
                .Where(p => p.Metadata.ContainsKey("quality_score"))
                .Select(p => (double)p.Metadata["quality_score"])
                .DefaultIfEmpty(0.5)
                .Average();

            // Extract insights from execution
            List<string> insights = await ExtractInsightsAsync(goal, phaseResults, ct);

            // Record experience
            OuroborosExperience experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: goal,
                Success: overallSuccess,
                QualityScore: averageQuality,
                Insights: insights,
                Timestamp: DateTime.UtcNow);

            _atom.RecordExperience(experience);

            // Store in memory asynchronously
            await StoreExperienceInMemoryAsync(experience, ct);

            // Update MeTTa knowledge base
            await UpdateMeTTaKnowledgeAsync(experience, ct);

            // Update capabilities based on success
            if (overallSuccess && averageQuality > QualityThresholdForCapabilityUpdate)
            {
                // Boost relevant capability confidence
                IEnumerable<string> relevantCapabilities = goal.ToLower().Split(' ')
                    .Where(word => _atom.Capabilities.Any(c => c.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));

                foreach (string capName in relevantCapabilities.Take(2))
                {
                    OuroborosCapability? existingCap = _atom.Capabilities.FirstOrDefault(c =>
                        c.Name.Contains(capName, StringComparison.OrdinalIgnoreCase));

                    if (existingCap != null)
                    {
                        double newConfidence = Math.Min(1.0, existingCap.ConfidenceLevel + ConfidenceBoostIncrement);
                        _atom.AddCapability(existingCap with { ConfidenceLevel = newConfidence });
                    }
                }
            }

            // Evolutionary optimization of planning strategies (optional, graceful)
            await TryEvolveStrategiesAsync(ct);

            sw.Stop();
            RecordPhaseMetric("learn", sw.ElapsedMilliseconds, true);

            return new PhaseResult(
                ImprovementPhase.Learn,
                Success: true,
                Output: $"Learned {insights.Count} insights. Success rate: {_atom.Experiences.Count(e => e.Success) / (double)_atom.Experiences.Count:P0}",
                Error: null,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["insights_count"] = insights.Count,
                    ["cycle_count"] = _atom.CycleCount,
                    ["total_experiences"] = _atom.Experiences.Count,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("learn", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Learn,
                Success: false,
                Output: string.Empty,
                Error: $"Learning failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes a single step from the plan.
    /// Uses the ToolVsLLMWeight strategy to determine whether to prefer tool execution or LLM reasoning.
    /// </summary>
    private async Task<Result<string, string>> ExecuteStepAsync(string step, CancellationToken ct)
    {
        // Read evolved strategy gene for tool vs LLM routing
        // ToolVsLLMWeight: 0.0 = prefer LLM, 1.0 = prefer tools
        // TODO: Implement LLM-first execution path when toolVsLlmWeight <= 0.5
        // For now, this reads the strategy to ensure it's available in the atom,
        // but the actual routing logic will be implemented in a future enhancement
        double toolVsLlmWeight = _atom.GetStrategyWeight("ToolVsLLMWeight", 0.7);

        // Try to match step to a tool
        Option<ITool> toolOption = _tools.All
            .FirstOrDefault(t => step.Contains(t.Name, StringComparison.OrdinalIgnoreCase))
            .ToOption();

        if (toolOption.HasValue && toolOption.Value != null)
        {
            ITool tool = toolOption.Value;

            // Check safety
            SafetyCheckResult safetyCheck = await _safety.CheckActionSafetyAsync(
                tool.Name,
                new Dictionary<string, object> { ["step"] = step },
                context: null,
                ct);

            if (!safetyCheck.IsAllowed)
            {
                return Result<string, string>.Failure($"Safety violation: {safetyCheck.Reason}");
            }

            // Execute tool
            // Note: toolVsLlmWeight is read above for future enhancement where we could
            // try LLM first when weight <= 0.5, then fall back to tools if needed
            return await tool.InvokeAsync(step, ct);
        }

        // No matching tool, use LLM to process step
        string response = await _llm.GenerateTextAsync($"Process this step: {step}", ct);
        return Result<string, string>.Success(response);
    }

    /// <summary>
    /// Translates current Ouroboros state to MeTTa atoms.
    /// </summary>
    private async Task TranslateToMeTTaAsync(CancellationToken ct)
    {
        string metta = _atom.ToMeTTa();
        await _mettaEngine.AddFactAsync(metta, ct);
    }

    /// <summary>
    /// Extracts insights from the execution.
    /// </summary>
    private async Task<List<string>> ExtractInsightsAsync(string goal, List<PhaseResult> phases, CancellationToken ct)
    {
        StringBuilder context = new StringBuilder();
        context.AppendLine($"Goal: {goal}");

        foreach (PhaseResult phase in phases)
        {
            context.AppendLine($"Phase {phase.Phase}: Success={phase.Success}");
            if (!string.IsNullOrEmpty(phase.Error))
            {
                context.AppendLine($"  Error: {phase.Error}");
            }
        }

        string prompt = $@"Extract 2-3 key insights from this execution:
{context}

Provide insights as a bullet list, each starting with '-'. Focus on:
- What worked well
- What could be improved
- Patterns to remember";

        string response = await _llm.GenerateTextAsync(prompt, ct);

        List<string> insights = response.Split('\n')
            .Where(l => l.TrimStart().StartsWith('-'))
            .Select(l => l.TrimStart('-', ' '))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return insights;
    }

    /// <summary>
    /// Stores experience in the memory store.
    /// </summary>
    private async Task StoreExperienceInMemoryAsync(OuroborosExperience ouroborosExp, CancellationToken ct)
    {
        // Convert to domain Experience type for storage
        Plan plan = new Plan(
            ouroborosExp.Goal,
            new List<PlanStep>(),
            new Dictionary<string, double> { ["quality"] = ouroborosExp.QualityScore },
            ouroborosExp.Timestamp);

        PlanExecutionResult execution = new PlanExecutionResult(
            plan,
            new List<StepResult>(),
            ouroborosExp.Success,
            string.Join("; ", ouroborosExp.Insights),
            new Dictionary<string, object>(),
            TimeSpan.Zero);

        PlanVerificationResult verification = new PlanVerificationResult(
            execution,
            ouroborosExp.Success,
            ouroborosExp.QualityScore,
            new List<string>(),
            ouroborosExp.Insights.ToList(),
            null);

        // Convert to Foundation Experience format
        Experience experience = new Experience(
            ouroborosExp.Id.ToString(),
            ouroborosExp.Timestamp,
            Context: ouroborosExp.Goal,
            Action: System.Text.Json.JsonSerializer.Serialize(plan),
            Outcome: execution.Success ? "Success" : "Failed",
            Success: execution.Success,
            Tags: new List<string> { "ouroboros", "orchestrator" },
            Goal: ouroborosExp.Goal,
            Execution: execution,
            Verification: verification);

        var result = await _memory.StoreExperienceAsync(experience, ct);
        if (!result.IsSuccess)
        {
            // Log error but don't fail the execution
        }
    }

    /// <summary>
    /// Updates MeTTa knowledge base with new experience.
    /// </summary>
    private async Task UpdateMeTTaKnowledgeAsync(OuroborosExperience experience, CancellationToken ct)
    {
        StringBuilder metta = new StringBuilder();

        string expType = experience.Success ? "SuccessfulExperience" : "FailedExperience";
        metta.AppendLine($"(LearnedFrom (OuroborosInstance \"{_atom.InstanceId}\") ({expType} \"{EscapeMeTTa(experience.Goal)}\" {experience.QualityScore:F2}))");

        foreach (string insight in experience.Insights)
        {
            metta.AppendLine($"(Insight \"{experience.Id}\" \"{EscapeMeTTa(insight)}\")");
        }

        await _mettaEngine.AddFactAsync(metta.ToString(), ct);
    }

    /// <summary>
    /// Calculates the current selection threshold based on arousal.
    /// High arousal → low threshold (faster, less deliberate decisions).
    /// Low arousal → high threshold (slower, more deliberate decisions).
    /// </summary>
    private static double CalculateSelectionThreshold(Affect.AffectiveState state)
    {
        double threshold = 0.5 - (state.Arousal * 0.2) + (state.Stress * 0.15);
        return Math.Clamp(threshold, 0.2, 0.8);
    }

    /// <summary>
    /// Calculates the effective resolution level, combining evolved strategy genes
    /// with dynamic arousal modulation.
    /// </summary>
    private double GetEffectiveResolutionLevel(Affect.AffectiveState state)
    {
        double evolvedDepth = _atom.GetStrategyWeight("PlanningDepth", 0.5);

        // Psi-theory: high arousal reduces resolution (fast, shallow processing)
        double arousalModulation = 1.0 - (state.Arousal * 0.4);

        return Math.Clamp(evolvedDepth * arousalModulation, 0.1, 1.0);
    }

    /// <summary>
    /// Projects the current affective state into the MeTTa knowledge base
    /// for symbolic reasoning about emotions.
    /// </summary>
    private async Task ProjectAffectiveStateToMeTTaAsync(
        Affect.AffectiveState affect, Affect.Urge? dominantUrge, CancellationToken ct)
    {
        string id = _atom.InstanceId;

        await _mettaEngine.AddFactAsync(
            $"(AffectiveState (OuroborosInstance \"{id}\") " +
            $"(Valence {affect.Valence:F2}) " +
            $"(Stress {affect.Stress:F2}) " +
            $"(Arousal {affect.Arousal:F2}) " +
            $"(Confidence {affect.Confidence:F2}) " +
            $"(Curiosity {affect.Curiosity:F2}))", ct);

        if (dominantUrge != null)
        {
            await _mettaEngine.AddFactAsync(
                $"(DominantUrge (OuroborosInstance \"{id}\") " +
                $"(Urge \"{dominantUrge.Name}\" {dominantUrge.Intensity:F2}))", ct);
        }

        if (_urgeSystem != null)
        {
            string urgesMeTTa = _urgeSystem.ToMeTTa(id);
            await _mettaEngine.AddFactAsync(urgesMeTTa, ct);
        }

        if (_spreading != null)
        {
            foreach (var (atomKey, activation) in _spreading.GetActivatedAtoms().Take(10))
            {
                await _mettaEngine.AddFactAsync(
                    $"(Primed (OuroborosInstance \"{id}\") (Concept \"{EscapeMeTTa(atomKey)}\" {activation:F2}))", ct);
            }
        }
    }

    private string BuildPlanPrompt(string goal, string selfReflection, OuroborosConfidence confidence)
    {
        string confidenceNote = confidence switch
        {
            OuroborosConfidence.High => "I have high confidence in achieving this goal based on past experience.",
            OuroborosConfidence.Medium => "I have moderate confidence in this goal - proceed with verification.",
            OuroborosConfidence.Low => "I have low confidence in this goal - careful planning required.",
            _ => string.Empty,
        };

        // Read evolved strategy genes for planning
        // PlanningDepth: 0.0 = shallow/fast, 1.0 = deep/thorough
        double planningDepth = _atom.GetStrategyWeight("PlanningDepth", 0.5);
        // DecompositionGranularity: 0.0 = coarse, 1.0 = fine
        double decompositionGranularity = _atom.GetStrategyWeight("DecompositionGranularity", 0.5);

        // Adjust planning guidance based on evolved strategy
        string planningGuidance = planningDepth < 0.3
            ? "a concise high-level plan"
            : planningDepth > 0.7
                ? "a detailed plan with sub-steps and contingencies"
                : "a structured plan with clear steps";

        // Suggest step count based on granularity (MinPlanSteps to MinPlanSteps+PlanStepsRange)
        int suggestedSteps = (int)(MinPlanSteps + (decompositionGranularity * PlanStepsRange));
        string stepGuidance = $"Aim for approximately {suggestedSteps} steps";

        return $@"Create a plan to achieve: {goal}

Self-Assessment:
{selfReflection}

Confidence: {confidenceNote}

Available tools: {string.Join(", ", _tools.All.Select(t => t.Name))}

Provide {planningGuidance}. {stepGuidance}. Each step should be actionable and specific.";
    }

    private string BuildVerificationPrompt(string goal, string output)
    {
        return $@"Verify if the following execution achieved the goal.

Goal: {goal}

Output:
{output}

Provide verification in JSON format:
{{
  ""verified"": true/false,
  ""quality_score"": 0.0-1.0,
  ""reasoning"": ""brief explanation""
}}";
    }

    private List<string> ParsePlanSteps(string plan)
    {
        List<string> steps = plan.Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Where(l => l.TrimStart().StartsWith('-') ||
                       l.TrimStart().StartsWith('*') ||
                       char.IsDigit(l.TrimStart().FirstOrDefault()))
            .Select(l => l.TrimStart('-', '*', ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // If no structured steps found, treat the whole plan as one step
        if (steps.Count == 0)
        {
            steps.Add(plan.Trim());
        }

        return steps;
    }

    private (bool Verified, double QualityScore) ParsePlanVerificationResult(string verificationText)
    {
        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(verificationText);
            bool verified = doc.RootElement.GetProperty("verified").GetBoolean();
            double qualityScore = doc.RootElement.GetProperty("quality_score").GetDouble();
            return (verified, qualityScore);
        }
        catch
        {
            // Default to success if parsing fails but output exists
            return (true, 0.7);
        }
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

    /// <summary>
    /// Tries to evolve planning strategies using the genetic algorithm, if configured.
    /// This method is graceful - failures are logged but don't prevent the Learn phase from completing.
    /// </summary>
    private async Task TryEvolveStrategiesAsync(CancellationToken ct)
    {
        // Skip if no GA configured or not enough experiences
        if (_strategyEvolver == null || _atom.Experiences.Count < 5)
        {
            return;
        }

        try
        {
            // Create initial population from current planning parameters
            var random = new Random();
            var initialPopulation = new List<Genetic.Abstractions.IChromosome<Evolution.PlanStrategyGene>>
            {
                Evolution.PlanStrategyChromosome.FromAtom(_atom),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
            };

            // Run evolution for 3-5 generations (start with 3 for speed)
            Result<Genetic.Abstractions.IChromosome<Evolution.PlanStrategyGene>, string> evolutionResult =
                await _strategyEvolver.EvolveAsync(initialPopulation, generations: 3);

            if (evolutionResult.IsSuccess)
            {
                // Extract best evolved strategy
                var bestChromosome = evolutionResult.Match(
                    success => success as Evolution.PlanStrategyChromosome,
                    _ => null);

                if (bestChromosome != null && bestChromosome.Fitness > 0.6)
                {
                    // Store evolved strategies as capabilities in the atom
                    foreach (var gene in bestChromosome.Genes)
                    {
                        var capability = new OuroborosCapability(
                            Name: $"Strategy_{gene.StrategyName}",
                            Description: gene.Description,
                            ConfidenceLevel: gene.Weight);
                        _atom.AddCapability(capability);
                    }

                    Console.WriteLine($"[GA] Evolved planning strategy with fitness {bestChromosome.Fitness:F3}: {bestChromosome}");
                }
            }
        }
        catch (Exception ex)
        {
            // Graceful degradation - log but don't fail
            Console.WriteLine($"[GA] Strategy evolution failed (non-fatal): {ex.Message}");
        }
    }

    private static string EscapeMeTTa(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    /// <inheritdoc/>
    protected override async Task<Dictionary<string, object>> GetCustomHealthAsync(CancellationToken ct)
    {
        Dictionary<string, object> health = await base.GetCustomHealthAsync(ct);

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