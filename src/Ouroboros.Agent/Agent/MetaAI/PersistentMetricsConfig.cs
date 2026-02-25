namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for persistent metrics storage.
/// </summary>
public sealed record PersistentMetricsConfig(
    string StoragePath = "metrics",
    string FileName = "performance_metrics.json",
    bool AutoSave = true,
    TimeSpan AutoSaveInterval = default,
    int MaxMetricsAge = 90);