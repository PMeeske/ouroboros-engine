
using System.Text.RegularExpressions;

namespace Ouroboros.Agent.MetaAI.MetaLearning;

public sealed partial class MetaLearner
{
    /// <summary>
    /// Analyzes learning history and optimizes learning strategy.
    /// </summary>
    public async Task<Result<LearningStrategy, string>> OptimizeLearningStrategyAsync(
        IReadOnlyList<LearningEpisode> history,
        CancellationToken ct = default)
    {
        try
        {
            if (history == null || history.Count < _config.MinEpisodesForOptimization)
            {
                return Result<LearningStrategy, string>.Failure(
                    $"Insufficient learning history. Need at least {_config.MinEpisodesForOptimization} episodes, got {history?.Count ?? 0}");
            }

            // Analyze successful vs failed episodes
            List<LearningEpisode> successful = history.Where(e => e.Successful).ToList();
            List<LearningEpisode> failed = history.Where(e => !e.Successful).ToList();

            if (!successful.Any())
            {
                return Result<LearningStrategy, string>.Failure("No successful episodes to learn from");
            }

            // Compute statistics
            double avgIterationsSuccess = successful.Average(e => e.IterationsRequired);
            double avgExamplesSuccess = successful.Average(e => e.ExamplesProvided);
            double avgPerformance = successful.Average(e => e.FinalPerformance);

            // Group by task type
            Dictionary<string, List<LearningEpisode>> byTaskType = successful
                .GroupBy(e => e.TaskType)
                .ToDictionary(g => g.Key, g => g.ToList());

            string mostSuccessfulTaskType = byTaskType
                .OrderByDescending(kvp => kvp.Value.Average(e => e.FinalPerformance))
                .First().Key;

            // Generate strategy using LLM
            string prompt = $@"Based on this learning history, suggest an optimized learning strategy:

OVERALL STATISTICS:
- Total episodes: {history.Count}
- Success rate: {(successful.Count / (double)history.Count):P0}
- Average iterations (successful): {avgIterationsSuccess:F1}
- Average examples needed: {avgExamplesSuccess:F1}
- Average performance: {avgPerformance:P0}

TASK TYPE BREAKDOWN:
{string.Join("\n", byTaskType.Select(kvp => $"- {kvp.Key}: {kvp.Value.Count} episodes, {kvp.Value.Average(e => e.FinalPerformance):P0} avg performance"))}

MOST SUCCESSFUL APPROACH:
- Task type: {mostSuccessfulTaskType}
- Strategy: {byTaskType[mostSuccessfulTaskType].First().StrategyUsed.Approach}

FAILURE ANALYSIS:
{(failed.Any() ? string.Join("\n", failed.Take(3).Select(e => $"- {e.TaskType}: {e.FailureReason}")) : "No failures")}

Recommend:
1. A strategy name (concise)
2. Learning approach (choose: Supervised, Reinforcement, SelfSupervised, ImitationLearning, CurriculumLearning, MetaGradient, PrototypicalLearning)
3. Suitable task types (comma-separated)
4. Expected efficiency improvement (percentage)
5. Key optimizations

Format:
NAME: [name]
APPROACH: [approach]
TASKS: [task types]
EFFICIENCY: [percentage]
OPTIMIZATIONS: [bullet points]";

            string response = await _llm.GenerateTextAsync(prompt, ct);
            LearningStrategy strategy = ParseStrategyResponse(response, successful);

            return Result<LearningStrategy, string>.Success(strategy);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<LearningStrategy, string>.Failure($"Strategy optimization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs few-shot adaptation to a new task.
    /// </summary>
    public async Task<Result<AdaptedModel, string>> FewShotAdaptAsync(
        string taskDescription,
        IReadOnlyList<TaskExample> examples,
        int maxExamples = 5,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskDescription))
            {
                return Result<AdaptedModel, string>.Failure("Task description cannot be empty");
            }

            if (examples == null || !examples.Any())
            {
                return Result<AdaptedModel, string>.Failure("At least one example is required for few-shot adaptation");
            }

            DateTime startTime = DateTime.UtcNow;

            // Limit examples to maxExamples, prioritizing by importance
            List<TaskExample> selectedExamples = examples
                .OrderByDescending(e => e.Importance ?? 1.0)
                .Take(Math.Min(maxExamples, _config.MaxFewShotExamples))
                .ToList();

            // Generate skill using few-shot learning with LLM
            string prompt = $@"Learn from these examples and create a reusable approach for this task:

TASK: {taskDescription}

EXAMPLES:
{string.Join("\n", selectedExamples.Select((e, i) => $"{i + 1}. Input: {e.Input}\n   Output: {e.ExpectedOutput}"))}

Extract:
1. Common patterns in the examples
2. Step-by-step approach to solve this task
3. Key transformations or logic
4. Prerequisites or constraints

Format:
PATTERNS: [list patterns]
STEPS:
1. [step]
2. [step]
...
PREREQUISITES: [list]
ESTIMATED_PERFORMANCE: [0-1]";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse response and create adapted skill
            AdaptedModel model = ParseAdaptationResponse(
                response,
                taskDescription,
                selectedExamples.Count,
                startTime);

            return Result<AdaptedModel, string>.Success(model);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<AdaptedModel, string>.Failure($"Few-shot adaptation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Suggests optimal hyperparameters for a given task type.
    /// </summary>
    public async Task<Result<HyperparameterConfig, string>> SuggestHyperparametersAsync(
        string taskType,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskType))
            {
                return Result<HyperparameterConfig, string>.Failure("Task type cannot be empty");
            }

            // Find relevant historical episodes
            List<LearningEpisode> relevantEpisodes = _episodes
                .Where(e => e.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase) && e.Successful)
                .OrderByDescending(e => e.FinalPerformance)
                .Take(10)
                .ToList();

            // Use defaults if no history
            if (!relevantEpisodes.Any())
            {
                return Result<HyperparameterConfig, string>.Success(CreateDefaultHyperparameters(taskType, context));
            }

            // Compute optimal parameters from successful episodes
            double avgLearningRate = 0.01; // Base rate
            int avgBatchSize = relevantEpisodes.Any() ? (int)relevantEpisodes.Average(e => e.ExamplesProvided) : 10;
            int avgMaxIterations = relevantEpisodes.Any() ? (int)relevantEpisodes.Average(e => e.IterationsRequired) : 50;
            double avgQualityThreshold = relevantEpisodes.Any() ? relevantEpisodes.Average(e => e.FinalPerformance) * 0.9 : 0.8;

            HyperparameterConfig config = new HyperparameterConfig(
                LearningRate: avgLearningRate,
                BatchSize: avgBatchSize,
                MaxIterations: avgMaxIterations,
                QualityThreshold: avgQualityThreshold,
                ExplorationRate: 0.1,
                CustomParams: context ?? new Dictionary<string, object>());

            return Result<HyperparameterConfig, string>.Success(config);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<HyperparameterConfig, string>.Failure($"Hyperparameter suggestion failed: {ex.Message}");
        }
    }

    private LearningStrategy ParseStrategyResponse(string response, List<LearningEpisode> successfulEpisodes)
    {
        string[] lines = response.Split('\n');
        string name = "Optimized Strategy";
        LearningApproach approach = LearningApproach.Supervised;
        List<string> taskTypes = new List<string> { "general" };
        double efficiency = 0.8;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("NAME:", StringComparison.OrdinalIgnoreCase))
            {
                name = trimmed.Substring("NAME:".Length).Trim();
            }
            else if (trimmed.StartsWith("APPROACH:", StringComparison.OrdinalIgnoreCase))
            {
                string approachStr = trimmed.Substring("APPROACH:".Length).Trim();
                if (Enum.TryParse<LearningApproach>(approachStr, true, out LearningApproach parsed))
                {
                    approach = parsed;
                }
            }
            else if (trimmed.StartsWith("TASKS:", StringComparison.OrdinalIgnoreCase))
            {
                string tasksStr = trimmed.Substring("TASKS:".Length).Trim();
                taskTypes = tasksStr.Split(',').Select(t => t.Trim()).ToList();
            }
            else if (trimmed.StartsWith("EFFICIENCY:", StringComparison.OrdinalIgnoreCase))
            {
                string effStr = trimmed.Substring("EFFICIENCY:".Length).Trim().TrimEnd('%');
                if (double.TryParse(effStr, out double eff))
                {
                    efficiency = eff / 100.0;
                }
            }
        }

        // Compute average hyperparameters from successful episodes
        HyperparameterConfig hyperparameters = new HyperparameterConfig(
            LearningRate: 0.01,
            BatchSize: (int)successfulEpisodes.Average(e => e.ExamplesProvided),
            MaxIterations: (int)successfulEpisodes.Average(e => e.IterationsRequired),
            QualityThreshold: successfulEpisodes.Average(e => e.FinalPerformance) * 0.9,
            ExplorationRate: 0.1,
            CustomParams: new Dictionary<string, object>());

        return new LearningStrategy(
            Name: name,
            Description: $"Optimized strategy based on {successfulEpisodes.Count} successful episodes",
            Approach: approach,
            Hyperparameters: hyperparameters,
            SuitableTaskTypes: taskTypes,
            ExpectedEfficiency: efficiency,
            CustomConfig: new Dictionary<string, object>());
    }

    private AdaptedModel ParseAdaptationResponse(
        string response,
        string taskDescription,
        int examplesUsed,
        DateTime startTime)
    {
        string[] lines = response.Split('\n');
        List<string> patterns = new List<string>();
        List<PlanStep> steps = new List<PlanStep>();
        double estimatedPerformance = 0.75;

        bool inPatterns = false;
        bool inSteps = false;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("PATTERNS:", StringComparison.OrdinalIgnoreCase))
            {
                inPatterns = true;
                inSteps = false;
                string patternsStr = trimmed.Substring("PATTERNS:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(patternsStr))
                {
                    patterns.AddRange(patternsStr.Split(',').Select(p => p.Trim()));
                }
            }
            else if (trimmed.StartsWith("STEPS:", StringComparison.OrdinalIgnoreCase))
            {
                inSteps = true;
                inPatterns = false;
            }
            else if (trimmed.StartsWith("ESTIMATED_PERFORMANCE:", StringComparison.OrdinalIgnoreCase))
            {
                string perfStr = trimmed.Substring("ESTIMATED_PERFORMANCE:".Length).Trim();
                if (double.TryParse(perfStr, out double perf))
                {
                    estimatedPerformance = perf;
                }

                inSteps = false;
                inPatterns = false;
            }
            else if (inPatterns && trimmed.StartsWith("-"))
            {
                patterns.Add(trimmed.TrimStart('-').Trim());
            }
            else if (inSteps && (trimmed.StartsWith("-") || char.IsDigit(trimmed.FirstOrDefault())))
            {
                string stepText = trimmed.TrimStart('-', ' ', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.');
                if (!string.IsNullOrWhiteSpace(stepText))
                {
                    steps.Add(new PlanStep(
                        Action: stepText,
                        Parameters: new Dictionary<string, object>(),
                        ExpectedOutcome: "Completed",
                        ConfidenceScore: estimatedPerformance));
                }
            }
        }

        // Create skill from extracted information
        string sanitizedTaskName = NonAlphanumericRegex().Replace(
            taskDescription.ToLowerInvariant(),
            "_");
        sanitizedTaskName = sanitizedTaskName[..Math.Min(20, sanitizedTaskName.Length)];

        Skill adaptedSkill = new Skill(
            Name: $"adapted_{sanitizedTaskName}_{startTime.Ticks}",
            Description: taskDescription,
            Prerequisites: new List<string>(),
            Steps: steps.Any() ? steps : new List<PlanStep>
            {
                new PlanStep("execute_task", new Dictionary<string, object>(), "Task completed", estimatedPerformance),
            },
            SuccessRate: estimatedPerformance,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow);

        double adaptationTime = (DateTime.UtcNow - startTime).TotalSeconds;

        return new AdaptedModel(
            TaskDescription: taskDescription,
            AdaptedSkill: adaptedSkill,
            ExamplesUsed: examplesUsed,
            EstimatedPerformance: estimatedPerformance,
            AdaptationTime: adaptationTime,
            LearnedPatterns: patterns.Any() ? patterns : new List<string> { "General task pattern" });
    }

    private HyperparameterConfig CreateDefaultHyperparameters(string taskType, Dictionary<string, object>? context = null)
    {
        var customParams = context ?? new Dictionary<string, object>();

        // Task-specific defaults
        return taskType.ToLowerInvariant() switch
        {
            "classification" => new HyperparameterConfig(0.01, 32, 100, 0.85, 0.05, customParams),
            "generation" => new HyperparameterConfig(0.001, 16, 200, 0.75, 0.15, customParams),
            "reasoning" => new HyperparameterConfig(0.005, 8, 150, 0.8, 0.1, customParams),
            _ => new HyperparameterConfig(0.01, 16, 100, 0.8, 0.1, customParams),
        };
    }

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonAlphanumericRegex();
}
