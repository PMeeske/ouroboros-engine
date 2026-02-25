// <copyright file="MetaLearning.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents a learning strategy configuration for meta-learning optimization.
/// Encapsulates hyperparameters that control the learning process.
/// </summary>
/// <param name="Id">Unique identifier for this strategy.</param>
/// <param name="Name">Human-readable name describing the strategy.</param>
/// <param name="LearningRate">Step size for gradient-based updates (typically 0.0001 to 0.1).</param>
/// <param name="ExplorationRate">Epsilon for exploration vs exploitation trade-off (0 = greedy, 1 = random).</param>
/// <param name="DiscountFactor">Gamma for future reward weighting (0 = myopic, 1 = far-sighted).</param>
/// <param name="BatchSize">Number of samples per learning update.</param>
/// <param name="Parameters">Additional strategy-specific parameters.</param>
public sealed record LearningStrategy(
    Guid Id,
    string Name,
    double LearningRate,
    double ExplorationRate,
    double DiscountFactor,
    int BatchSize,
    ImmutableDictionary<string, double> Parameters)
{
    /// <summary>
    /// Gets the default learning strategy with sensible initial hyperparameters.
    /// Uses Adam-like defaults: LR=0.001, ε=0.1, γ=0.99, batch=32.
    /// </summary>
    public static LearningStrategy Default => new(
        Id: Guid.NewGuid(),
        Name: "Default",
        LearningRate: 0.001,
        ExplorationRate: 0.1,
        DiscountFactor: 0.99,
        BatchSize: 32,
        Parameters: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Creates a new strategy with exploration-focused parameters.
    /// Higher exploration rate for initial learning phases.
    /// </summary>
    /// <param name="name">Optional name for the strategy.</param>
    /// <returns>An exploration-focused learning strategy.</returns>
    public static LearningStrategy Exploratory(string name = "Exploratory") => new(
        Id: Guid.NewGuid(),
        Name: name,
        LearningRate: 0.01,
        ExplorationRate: 0.5,
        DiscountFactor: 0.95,
        BatchSize: 64,
        Parameters: ImmutableDictionary<string, double>.Empty
            .Add("temperature", 1.5)
            .Add("curiosity_weight", 0.3));

    /// <summary>
    /// Creates a new strategy optimized for exploitation and fine-tuning.
    /// Lower exploration rate for converged policies.
    /// </summary>
    /// <param name="name">Optional name for the strategy.</param>
    /// <returns>An exploitation-focused learning strategy.</returns>
    public static LearningStrategy Exploitative(string name = "Exploitative") => new(
        Id: Guid.NewGuid(),
        Name: name,
        LearningRate: 0.0001,
        ExplorationRate: 0.01,
        DiscountFactor: 0.999,
        BatchSize: 128,
        Parameters: ImmutableDictionary<string, double>.Empty
            .Add("temperature", 0.5)
            .Add("curiosity_weight", 0.05));

    /// <summary>
    /// Creates a copy with adjusted learning rate.
    /// </summary>
    /// <param name="newLearningRate">The new learning rate value.</param>
    /// <returns>A new strategy with the updated learning rate.</returns>
    public LearningStrategy WithLearningRate(double newLearningRate)
        => this with { LearningRate = Math.Clamp(newLearningRate, 1e-7, 1.0) };

    /// <summary>
    /// Creates a copy with adjusted exploration rate.
    /// </summary>
    /// <param name="newExplorationRate">The new exploration rate value.</param>
    /// <returns>A new strategy with the updated exploration rate.</returns>
    public LearningStrategy WithExplorationRate(double newExplorationRate)
        => this with { ExplorationRate = Math.Clamp(newExplorationRate, 0.0, 1.0) };

    /// <summary>
    /// Creates a copy with adjusted discount factor.
    /// </summary>
    /// <param name="newDiscountFactor">The new discount factor value.</param>
    /// <returns>A new strategy with the updated discount factor.</returns>
    public LearningStrategy WithDiscountFactor(double newDiscountFactor)
        => this with { DiscountFactor = Math.Clamp(newDiscountFactor, 0.0, 1.0) };

    /// <summary>
    /// Creates a copy with an additional or updated parameter.
    /// </summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>A new strategy with the updated parameter.</returns>
    public LearningStrategy WithParameter(string key, double value)
        => this with { Parameters = Parameters.SetItem(key, value) };

    /// <summary>
    /// Validates the strategy parameters.
    /// </summary>
    /// <returns>A Result indicating success or validation errors.</returns>
    public Result<Unit, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Result<Unit, string>.Failure("Strategy name cannot be empty.");
        }

        if (LearningRate <= 0 || LearningRate > 1)
        {
            return Result<Unit, string>.Failure($"Learning rate must be in (0, 1], got {LearningRate}.");
        }

        if (ExplorationRate < 0 || ExplorationRate > 1)
        {
            return Result<Unit, string>.Failure($"Exploration rate must be in [0, 1], got {ExplorationRate}.");
        }

        if (DiscountFactor < 0 || DiscountFactor > 1)
        {
            return Result<Unit, string>.Failure($"Discount factor must be in [0, 1], got {DiscountFactor}.");
        }

        if (BatchSize <= 0)
        {
            return Result<Unit, string>.Failure($"Batch size must be positive, got {BatchSize}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}