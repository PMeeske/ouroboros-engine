namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Status of an experiment.
/// </summary>
public enum ExperimentStatus
{
    /// <summary>Experiment is currently running.</summary>
    Running,

    /// <summary>Experiment completed successfully.</summary>
    Completed,

    /// <summary>Experiment was cancelled.</summary>
    Cancelled,

    /// <summary>Experiment failed due to an error.</summary>
    Failed
}