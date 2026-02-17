namespace Ouroboros.Providers;

/// <summary>
/// Types of election events.
/// </summary>
public enum ElectionEventType
{
    ElectionStarted,
    CandidateEvaluated,
    MasterEvaluation,
    MasterEvaluationFailed,
    ElectionComplete,
    OptimizationSuggested
}