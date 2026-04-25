// ==========================================================
// Curiosity Engine Implementation
// Intrinsic motivation and autonomous exploration
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of curiosity-driven exploration.
/// </summary>
public sealed partial class CuriosityEngine : ICuriosityEngine
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly ISafetyGuard _safety;
    private readonly Core.Ethics.IEthicsFramework _ethics;
    private readonly CuriosityEngineConfig _config;
    private readonly ConcurrentBag<(Plan plan, double novelty, DateTime when)> _explorationHistory = new();
    private int _totalExplorations = 0;
    private int _sessionExplorations = 0;

    public CuriosityEngine(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        IMemoryStore memory,
        ISkillRegistry skills,
        ISafetyGuard safety,
        Core.Ethics.IEthicsFramework ethics,
        CuriosityEngineConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        ArgumentNullException.ThrowIfNull(skills);
        _skills = skills;
        ArgumentNullException.ThrowIfNull(safety);
        _safety = safety;
        ArgumentNullException.ThrowIfNull(ethics);
        _ethics = ethics;
        _config = config ?? new CuriosityEngineConfig();
    }

    /// <summary>
    /// Computes the novelty score for a potential action or plan.
    /// </summary>
    public async Task<double> ComputeNoveltyAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        if (plan == null)
            return 0.0;

        try
        {
            // Check similarity to past experiences
            MemoryQuery query = new MemoryQuery(
                Tags: null,
                ContextSimilarity: plan.Goal,
                SuccessOnly: null,
                FromDate: null,
                ToDate: null,
                MaxResults: 20);

            var experiencesResult = await _memory.QueryExperiencesAsync(query, ct).ConfigureAwait(false);

            if (!experiencesResult.IsSuccess)
                return 0.5; // Default moderate novelty on query failure

            IReadOnlyList<Experience> experiences = experiencesResult.Value;

            if (!experiences.Any())
                return 1.0; // Completely novel - no similar experiences

            // Calculate average similarity to past experiences
            List<double> similarities = new List<double>();

            foreach (Experience exp in experiences)
            {
                if (exp.Plan is null) continue;
                double similarity = CalculateActionSimilarity(plan, exp.Plan);
                similarities.Add(similarity);
            }

            double avgSimilarity = similarities.Average();
            double novelty = 1.0 - avgSimilarity; // Higher novelty when less similar to past

            return Math.Clamp(novelty, 0.0, 1.0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return 0.5; // Default moderate novelty
        }
    }

    /// <summary>
    /// Generates an exploratory plan to learn something new.
    /// </summary>
    public async Task<Result<Plan, string>> GenerateExploratoryPlanAsync(
        CancellationToken ct = default)
    {
        try
        {
            // Identify unexplored areas
            List<ExplorationOpportunity> opportunities = await IdentifyExplorationOpportunitiesAsync(5, ct).ConfigureAwait(false);

            if (!opportunities.Any())
            {
                return Result<Plan, string>.Failure("No exploration opportunities identified");
            }

            // Pick the most promising opportunity
            ExplorationOpportunity bestOpportunity = opportunities.OrderByDescending(o => o.InformationGainEstimate).First();

            // Generate plan for exploration
            string prompt = $@"Create an exploratory plan for learning:

Exploration ContextSimilarity: {bestOpportunity.Description}
Expected Information Gain: {bestOpportunity.InformationGainEstimate:P0}
Novelty: {bestOpportunity.NoveltyScore:P0}

Design a safe, structured exploration that will:
1. Maximize learning about this new area
2. Stay within safety boundaries
3. Build upon existing capabilities

Create 3-5 concrete steps for exploration.

Format:
STEP 1: [action]
EXPECTED: [what we'll learn]
CONFIDENCE: [0-1]

STEP 2: ...";

            string response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            // Parse plan
            List<PlanStep> steps = ParseExploratorySteps(response);

            // Safety check all steps
            if (_config.EnableSafeExploration)
            {
                foreach (PlanStep step in steps)
                {
                    SafetyCheckResult safetyCheck = _safety.CheckSafety(
                        step.Action,
                        step.Parameters,
                        PermissionLevel.UserDataWithConfirmation);

                    if (!safetyCheck.Safe)
                    {
                        return Result<Plan, string>.Failure(
                            $"Exploration plan failed safety check: {string.Join(", ", safetyCheck.Violations)}");
                    }
                }
            }

            Plan plan = new Plan(
                $"Explore: {bestOpportunity.Description}",
                steps,
                new Dictionary<string, double>
                {
                    ["exploratory"] = 1.0,
                    ["novelty"] = bestOpportunity.NoveltyScore
                },
                DateTime.UtcNow);

            // Ethics evaluation - validate exploration before proceeding
            var researchDescription = $"Exploratory research: {bestOpportunity.Description}";

            var context = new Core.Ethics.ActionContext
            {
                AgentId = "curiosity-engine",
                UserId = null,
                Environment = "exploration",
                State = new Dictionary<string, object>
                {
                    ["novelty_score"] = bestOpportunity.NoveltyScore,
                    ["info_gain_estimate"] = bestOpportunity.InformationGainEstimate,
                    ["step_count"] = steps.Count
                }
            };

            var ethicsResult = await _ethics.EvaluateResearchAsync(researchDescription, context, ct).ConfigureAwait(false);

            if (ethicsResult.IsFailure)
            {
                return Result<Plan, string>.Failure(
                    $"Exploration rejected by ethics evaluation: {ethicsResult.Error}");
            }

            if (!ethicsResult.Value.IsPermitted)
            {
                return Result<Plan, string>.Failure(
                    $"Exploration rejected by ethics framework: {ethicsResult.Value.Reasoning}");
            }

            if (ethicsResult.Value.Level == Core.Ethics.EthicalClearanceLevel.RequiresHumanApproval)
            {
                return Result<Plan, string>.Failure(
                    $"Exploration requires human approval before execution: {ethicsResult.Value.Reasoning}");
            }

            return Result<Plan, string>.Success(plan);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Plan, string>.Failure($"Exploratory plan generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Decides whether to explore or exploit based on current state.
    /// </summary>
    public async Task<bool> ShouldExploreAsync(
        string? currentGoal = null,
        CancellationToken ct = default)
    {
        // Check session exploration limit
        if (_sessionExplorations >= _config.MaxExplorationPerSession)
            return false;

        // Calculate exploration probability using epsilon-greedy strategy
        Random random = Random.Shared;
        double explorationProbability = 1.0 - _config.ExploitationBias;

        // Increase exploration if we haven't explored much recently
        int recentExplorations = _explorationHistory
            .Count(e => e.when > DateTime.UtcNow.AddHours(-24));

        if (recentExplorations < 5)
        {
            explorationProbability += 0.2;
        }

        // If we have a goal, check if it's novel enough
        if (!string.IsNullOrWhiteSpace(currentGoal))
        {
            Plan goalPlan = new Plan(currentGoal, new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
            double novelty = await ComputeNoveltyAsync(goalPlan, ct).ConfigureAwait(false);

            if (novelty > _config.ExplorationThreshold)
            {
                // Goal is already novel, no need for additional exploration
                return false;
            }
        }

        return random.NextDouble() < explorationProbability;
    }

    /// <summary>
    /// Estimates the information gain from exploring a particular area.
    /// </summary>
    public async Task<double> EstimateInformationGainAsync(
        string explorationDescription,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(explorationDescription))
            return 0.0;

        try
        {
            // Check how much we already know about this area
            MemoryQuery query = new MemoryQuery(
                Tags: null,
                ContextSimilarity: explorationDescription,
                SuccessOnly: null,
                FromDate: null,
                ToDate: null,
                MaxResults: 10);

            var experiencesResult = await _memory.QueryExperiencesAsync(query, ct).ConfigureAwait(false);

            if (!experiencesResult.IsSuccess)
                return 0.5; // Default moderate potential on query failure

            IReadOnlyList<Experience> experiences = experiencesResult.Value;

            // Less knowledge = higher potential information gain
            if (!experiences.Any())
                return 0.9; // High potential

            double coverage = Math.Min(experiences.Count / 10.0, 1.0);
            double informationGain = 1.0 - (coverage * 0.7); // Some gain even with knowledge

            return Math.Clamp(informationGain, 0.1, 1.0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return 0.5; // Default moderate gain
        }
    }

    /// <summary>
    /// Records the outcome of an exploration attempt.
    /// </summary>
    public void RecordExploration(Plan plan, PlanExecutionResult execution, double actualNovelty)
    {
        if (plan == null || execution == null)
            return;

        _explorationHistory.Add((plan, actualNovelty, DateTime.UtcNow));
        Interlocked.Increment(ref _totalExplorations);
        Interlocked.Increment(ref _sessionExplorations);
    }

    /// <summary>
    /// Gets exploration statistics.
    /// </summary>
    public Dictionary<string, double> GetExplorationStats()
    {
        Dictionary<string, double> stats = new Dictionary<string, double>();

        stats["total_explorations"] = _totalExplorations;
        stats["session_explorations"] = _sessionExplorations;

        List<(Plan plan, double novelty, DateTime when)> recent = _explorationHistory
            .Where(e => e.when > DateTime.UtcNow.AddDays(-7))
            .ToList();

        stats["explorations_last_week"] = recent.Count;
        stats["avg_novelty"] = recent.Any() ? recent.Average(e => e.novelty) : 0.0;

        return stats;
    }

}
