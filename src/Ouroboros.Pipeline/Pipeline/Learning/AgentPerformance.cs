// <copyright file="AdaptiveAgent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Tracks agent performance metrics over time.
/// Provides immutable snapshots of agent capability and learning progress.
/// </summary>
/// <param name="AgentId">Unique identifier for the agent being tracked.</param>
/// <param name="TotalInteractions">Total number of interactions processed by the agent.</param>
/// <param name="SuccessRate">Ratio of successful interactions (0.0 to 1.0).</param>
/// <param name="AverageResponseQuality">Mean quality score of responses (-1.0 to 1.0).</param>
/// <param name="LearningCurve">Historical performance values over time for trend analysis.</param>
/// <param name="LastUpdated">Timestamp of the most recent metric update.</param>
public sealed record AgentPerformance(
    Guid AgentId,
    long TotalInteractions,
    double SuccessRate,
    double AverageResponseQuality,
    ImmutableList<double> LearningCurve,
    DateTime LastUpdated)
{
    /// <summary>
    /// Creates initial performance metrics for a new agent.
    /// </summary>
    /// <param name="agentId">The unique identifier for the agent.</param>
    /// <returns>An AgentPerformance instance with zeroed metrics.</returns>
    public static AgentPerformance Initial(Guid agentId) => new(
        AgentId: agentId,
        TotalInteractions: 0,
        SuccessRate: 0.0,
        AverageResponseQuality: 0.0,
        LearningCurve: ImmutableList<double>.Empty,
        LastUpdated: DateTime.UtcNow);

    /// <summary>
    /// Creates a performance snapshot with an updated learning curve entry.
    /// </summary>
    /// <param name="currentPerformance">The current performance value to record.</param>
    /// <param name="maxCurveLength">Maximum length of the learning curve history (default: 100).</param>
    /// <returns>A new AgentPerformance with the updated learning curve.</returns>
    public AgentPerformance WithLearningCurveEntry(double currentPerformance, int maxCurveLength = 100)
    {
        var newCurve = LearningCurve.Add(currentPerformance);

        // Trim curve if it exceeds maximum length
        if (newCurve.Count > maxCurveLength)
        {
            newCurve = newCurve.RemoveRange(0, newCurve.Count - maxCurveLength);
        }

        return this with
        {
            LearningCurve = newCurve,
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Calculates the trend of recent performance (positive = improving, negative = declining).
    /// Uses the slope of the last N entries in the learning curve.
    /// </summary>
    /// <param name="windowSize">Number of recent entries to consider (default: 10).</param>
    /// <returns>The performance trend as a slope value.</returns>
    public double CalculateTrend(int windowSize = 10)
    {
        if (LearningCurve.Count < 2)
        {
            return 0.0;
        }

        var effectiveWindow = Math.Min(windowSize, LearningCurve.Count);
        var recentValues = LearningCurve.Skip(LearningCurve.Count - effectiveWindow).ToList();

        // Simple linear regression slope
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < recentValues.Count; i++)
        {
            sumX += i;
            sumY += recentValues[i];
            sumXY += i * recentValues[i];
            sumX2 += i * i;
        }

        var n = recentValues.Count;
        var denominator = (n * sumX2) - (sumX * sumX);
        return denominator == 0 ? 0.0 : ((n * sumXY) - (sumX * sumY)) / denominator;
    }

    /// <summary>
    /// Determines if performance is stagnating based on variance in recent performance.
    /// </summary>
    /// <param name="windowSize">Number of recent entries to analyze.</param>
    /// <param name="varianceThreshold">Threshold below which performance is considered stagnant.</param>
    /// <returns>True if performance shows signs of stagnation.</returns>
    public bool IsStagnating(int windowSize = 10, double varianceThreshold = 0.001)
    {
        if (LearningCurve.Count < windowSize)
        {
            return false;
        }

        var recentValues = LearningCurve.Skip(LearningCurve.Count - windowSize).ToList();
        var mean = recentValues.Average();
        var variance = recentValues.Sum(v => Math.Pow(v - mean, 2)) / windowSize;

        return variance < varianceThreshold;
    }
}