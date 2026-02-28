// ==========================================================
// Meta-AI Layer v3.0 - MeTTa-First Orchestrator
// Integrates symbolic reasoning with neural planning
// Now with Laws of Form integration for distinction-gated reasoning
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;
using Unit = Ouroboros.Abstractions.Unit;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Meta-AI v3.0 orchestrator with MeTTa-first representation layer.
/// Mirrors all orchestration concepts as MeTTa atoms and uses symbolic reasoning for next-node selection.
/// Supports Laws of Form integration for distinction-gated inference.
/// </summary>
public sealed partial class MeTTaOrchestrator : IMetaAIPlannerOrchestrator
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly IUncertaintyRouter _router;
    private readonly ISafetyGuard _safety;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly MeTTaRepresentation _representation;
    private readonly FormMeTTaBridge? _formBridge;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();

    /// <summary>
    /// Gets the Laws of Form bridge if enabled.
    /// </summary>
    public FormMeTTaBridge? FormBridge => _formBridge;

    /// <summary>
    /// Gets whether Laws of Form reasoning is enabled.
    /// </summary>
    public bool FormReasoningEnabled => _formBridge != null;

    public MeTTaOrchestrator(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISkillRegistry skills,
        IUncertaintyRouter router,
        ISafetyGuard safety,
        IMeTTaEngine mettaEngine,
        FormMeTTaBridge? formBridge = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _representation = new MeTTaRepresentation(mettaEngine, formBridge);
        _formBridge = formBridge;

        // Subscribe to form reasoning events if bridge is available
        if (_formBridge != null)
        {
            _formBridge.DistinctionChanged += OnDistinctionChanged;
            _formBridge.TruthValueEvaluated += OnTruthValueEvaluated;
        }
    }

    private void OnDistinctionChanged(object? sender, DistinctionEventArgs e)
    {
        // Track distinction events for metrics
        RecordMetric($"form_distinction_{e.EventType}", 1.0, true);
    }

    private void OnTruthValueEvaluated(object? sender, TruthValueEventArgs e)
    {
        // Track certainty evaluations
        string certainty = e.TruthValue.IsMarked() ? "certain" : e.TruthValue.IsVoid() ? "negated" : "uncertain";
        RecordMetric($"form_certainty_{certainty}", 1.0, true);
    }

    /// <summary>
    /// Draws a distinction in the given context (requires FormBridge).
    /// </summary>
    /// <param name="context">The context name.</param>
    /// <returns>The resulting form, or null if FormBridge not available.</returns>
    public Form? DrawDistinction(string context)
    {
        return _formBridge?.DrawDistinction(context);
    }

    /// <summary>
    /// Evaluates whether a step should proceed based on distinction certainty.
    /// </summary>
    /// <param name="context">The distinction context to check.</param>
    /// <returns>True if the distinction is marked (certain), false otherwise.</returns>
    public bool IsDistinctionCertain(string context)
    {
        if (_formBridge == null) return true; // No form reasoning = always proceed

        var form = _formBridge.EvaluateTruthValue(Atom.Sym(context));
        return form.IsMarked();
    }

    /// <summary>
    /// Plans with MeTTa symbolic representation.
    /// </summary>
    public async Task<Result<Plan, string>> PlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return Result<Plan, string>.Failure("Goal cannot be empty");

        try
        {
            Stopwatch sw = Stopwatch.StartNew();

                        // Get past experiences and skills
            MemoryQuery query = MemoryQueryExtensions.ForGoal(goal, context, maxResults: 5, minSimilarity: 0.7);
            var experiencesResult = await _memory.RetrieveRelevantExperiencesAsync(query, ct);
            List<Experience> pastExperiences = experiencesResult.IsSuccess ? experiencesResult.Value.ToList() : new List<Experience>();
            List<Skill> matchingSkills = await _skills.FindMatchingSkillsAsync(goal, context);

            // Generate initial plan using LLM
            string planPrompt = BuildPlanPrompt(goal, context, pastExperiences, matchingSkills);
            string planText = await _llm.GenerateTextAsync(planPrompt, ct);
            Plan plan = ParsePlan(planText, goal);

            // Translate plan to MeTTa representation
            Result<Unit, string> translationResult = await _representation.TranslatePlanAsync(plan, ct);
            if (translationResult.IsFailure)
            {
                Trace.TraceWarning("Failed to translate plan to MeTTa: {0}", translationResult.Error);
            }

            // Translate tools to MeTTa
            Result<Unit, string> toolTranslation = await _representation.TranslateToolsAsync(_tools, ct);
            if (toolTranslation.IsFailure)
            {
                Trace.TraceWarning("Failed to translate tools to MeTTa: {0}", toolTranslation.Error);
            }

            // Validate plan safety
            foreach (PlanStep step in plan.Steps)
            {
                SafetyCheckResult safetyCheck = _safety.CheckSafety(
                    step.Action,
                    step.Parameters,
                    PermissionLevel.UserDataWithConfirmation);

                if (!safetyCheck.Safe)
                {
                    return Result<Plan, string>.Failure(
                        $"Plan step '{step.Action}' failed safety check: {string.Join(", ", safetyCheck.Violations)}");
                }
            }

            RecordMetric("planner", sw.ElapsedMilliseconds, true);
            return Result<Plan, string>.Success(plan);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            RecordMetric("planner", 1.0, false);
            return Result<Plan, string>.Failure($"Planning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes plan with symbolic next-node selection.
    /// </summary>
    public async Task<Result<PlanExecutionResult, string>> ExecuteAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        List<StepResult> stepResults = new List<StepResult>();
        Stopwatch sw = Stopwatch.StartNew();
        Dictionary<string, object> metadata = new Dictionary<string, object>();

        try
        {
            // Execute each step with MeTTa-guided selection
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                PlanStep step = plan.Steps[i];
                string stepId = $"step_{i}";

                // Query MeTTa for next node validation
                Dictionary<string, object> context = new Dictionary<string, object>
                {
                    ["step_index"] = i,
                    ["total_steps"] = plan.Steps.Count
                };

                if (i > 0)
                {
                    // Use MeTTa to validate this is a valid next step
                    Result<List<NextNodeCandidate>, string> nextNodes = await _representation.QueryNextNodesAsync(
                        $"step_{i - 1}",
                        context,
                        ct
                    );

                    if (nextNodes.IsSuccess)
                    {
                        bool validNext = nextNodes.Value.Any(n => n.NodeId == stepId);
                        metadata[$"step_{i}_metta_validated"] = validNext;
                    }
                }

                // Execute the step
                StepResult stepResult = await ExecuteStepAsync(step, ct);
                stepResults.Add(stepResult);

                // Update MeTTa with execution results
                PlanExecutionResult execResult = new PlanExecutionResult(
                    plan,
                    stepResults.ToList(),
                    stepResult.Success,
                    stepResult.Output,
                    metadata,
                    sw.Elapsed
                );

                await _representation.TranslateExecutionStateAsync(execResult, ct);

                if (!stepResult.Success && !string.IsNullOrEmpty(stepResult.Error))
                {
                    RecordMetric("executor", sw.ElapsedMilliseconds, false);
                    return Result<PlanExecutionResult, string>.Success(
                        new PlanExecutionResult(plan, stepResults, false, stepResult.Error, metadata, sw.Elapsed));
                }
            }

            sw.Stop();
            RecordMetric("executor", sw.ElapsedMilliseconds, true);

            string finalOutput = stepResults.LastOrDefault()?.Output ?? string.Empty;
            return Result<PlanExecutionResult, string>.Success(
                new PlanExecutionResult(plan, stepResults, true, finalOutput, metadata, sw.Elapsed));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            RecordMetric("executor", sw.ElapsedMilliseconds, false);
            return Result<PlanExecutionResult, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies execution with MeTTa symbolic reasoning.
    /// </summary>
    public async Task<Result<PlanVerificationResult, string>> VerifyAsync(
        PlanExecutionResult execution,
        CancellationToken ct = default)
    {
        try
        {
            Stopwatch sw = Stopwatch.StartNew();

            // Build verification prompt
            string verifyPrompt = BuildVerificationPrompt(execution);
            string verificationText = await _llm.GenerateTextAsync(verifyPrompt, ct);

            // Parse verification result
            PlanVerificationResult verification = ParseVerification(execution, verificationText);

            // Use MeTTa for symbolic plan verification if available
            string planMetta = FormatPlanForMeTTa(execution.Plan);
            Result<bool, string> mettaVerification = await _mettaEngine.VerifyPlanAsync(planMetta, ct);

            if (mettaVerification.IsSuccess)
            {
                verification = verification with
                {
                    Improvements = verification.Improvements
                        .Append($"MeTTa verification: {(mettaVerification.Value ? "PASSED" : "FAILED")}")
                        .ToList()
                };
            }

            sw.Stop();
            RecordMetric("verifier", sw.ElapsedMilliseconds, true);

            return Result<PlanVerificationResult, string>.Success(verification);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            RecordMetric("verifier", 1.0, false);
            return Result<PlanVerificationResult, string>.Failure($"Verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Learns from execution and updates MeTTa knowledge base.
    /// </summary>
    public void LearnFromExecution(PlanVerificationResult verification)
    {
                // Create experience for memory using factory
        Experience experience = ExperienceFactory.FromExecution(
            goal: verification.Execution.Plan.Goal,
            execution: verification.Execution,
            verification: verification,
            metadata: verification.Execution.Metadata);

        // Store in memory asynchronously
        _ = _memory.StoreExperienceAsync(experience);

        // Update metrics
        RecordMetric("learner", 1.0, verification.Verified);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
    {
        return _metrics;
    }

}
