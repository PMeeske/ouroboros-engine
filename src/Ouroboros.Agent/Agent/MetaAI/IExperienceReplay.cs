namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for experience replay capabilities.
/// </summary>
public interface IExperienceReplay
{
    /// <summary>
    /// Trains the orchestrator on stored experiences.
    /// </summary>
    Task<Result<TrainingResult, string>> TrainOnExperiencesAsync(
        ExperienceReplayConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Analyzes experiences to extract patterns.
    /// </summary>
    Task<List<string>> AnalyzeExperiencePatternsAsync(
        List<Experience> experiences,
        CancellationToken ct = default);

    /// <summary>
    /// Selects experiences for training based on priority.
    /// </summary>
    Task<List<Experience>> SelectTrainingExperiencesAsync(
        ExperienceReplayConfig config,
        CancellationToken ct = default);
}