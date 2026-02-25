namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Performance metrics for the agent.
/// </summary>
public sealed record AgentPerformance(
    double OverallSuccessRate,
    double AverageResponseTime,
    int TotalTasks,
    int SuccessfulTasks,
    int FailedTasks,
    Dictionary<string, double> CapabilitySuccessRates,
    Dictionary<string, double> ResourceUtilization,
    DateTime MeasurementPeriodStart,
    DateTime MeasurementPeriodEnd);