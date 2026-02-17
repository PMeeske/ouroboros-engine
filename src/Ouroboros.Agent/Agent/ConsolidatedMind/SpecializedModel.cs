namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Represents a specialized sub-model within the ConsolidatedMind architecture.
/// Each specialist has a defined role, capabilities, and underlying model.
/// </summary>
/// <param name="Role">The cognitive role this specialist fulfills.</param>
/// <param name="Model">The underlying chat completion model.</param>
/// <param name="ModelName">Human-readable name for the model.</param>
/// <param name="Capabilities">List of specific capabilities this specialist excels at.</param>
/// <param name="Priority">Priority weight for selection (higher = preferred).</param>
/// <param name="MaxTokens">Maximum tokens this model can handle.</param>
/// <param name="CostPerToken">Relative cost per token (for budget optimization).</param>
/// <param name="AverageLatencyMs">Expected average latency in milliseconds.</param>
public sealed record SpecializedModel(
    SpecializedRole Role,
    Ouroboros.Abstractions.Core.IChatCompletionModel Model,
    string ModelName,
    string[] Capabilities,
    double Priority = 1.0,
    int MaxTokens = 4096,
    double CostPerToken = 0.0,
    double AverageLatencyMs = 500.0)
{
    /// <summary>
    /// Calculates a fitness score for this specialist given a task description.
    /// </summary>
    /// <param name="taskCapabilities">Required capabilities for the task.</param>
    /// <returns>A fitness score between 0 and 1.</returns>
    public double CalculateFitness(string[] taskCapabilities)
    {
        if (taskCapabilities == null || taskCapabilities.Length == 0)
            return 0.5; // Default fitness for unknown tasks

        int matches = taskCapabilities.Count(tc =>
            Capabilities.Any(c => c.Equals(tc, StringComparison.OrdinalIgnoreCase)));

        double capabilityScore = (double)matches / taskCapabilities.Length;
        return capabilityScore * Priority;
    }
}