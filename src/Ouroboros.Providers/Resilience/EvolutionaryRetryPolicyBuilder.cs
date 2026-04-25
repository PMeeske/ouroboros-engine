// <copyright file="EvolutionaryRetryPolicyBuilder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Fluent builder for constructing <see cref="EvolutionaryRetryPolicy{TContext}"/> instances.
/// </summary>
public sealed class EvolutionaryRetryPolicyBuilder<TContext>
    where TContext : class
{
    private readonly List<IMutationStrategy<TContext>> _strategies = [];
    private int _maxGenerations = 5;
    private ILogger? _logger;
    private ToolCallMutationChromosome? _chromosome;
    private ToolCallMutationFitness? _fitnessFunction;

    /// <summary>
    /// Adds a mutation strategy to the policy.
    /// </summary>
    /// <returns></returns>
    public EvolutionaryRetryPolicyBuilder<TContext> WithStrategy(IMutationStrategy<TContext> strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategies.Add(strategy);
        return this;
    }

    /// <summary>
    /// Sets the maximum number of evolutionary generations.
    /// </summary>
    /// <returns></returns>
    public EvolutionaryRetryPolicyBuilder<TContext> WithMaxGenerations(int maxGenerations)
    {
        _maxGenerations = maxGenerations;
        return this;
    }

    /// <summary>
    /// Sets the logger for the policy.
    /// </summary>
    /// <returns></returns>
    public EvolutionaryRetryPolicyBuilder<TContext> WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Sets the initial chromosome for GA-based strategy selection.
    /// </summary>
    /// <returns></returns>
    public EvolutionaryRetryPolicyBuilder<TContext> WithChromosome(ToolCallMutationChromosome chromosome)
    {
        ArgumentNullException.ThrowIfNull(chromosome);
        _chromosome = chromosome;
        return this;
    }

    /// <summary>
    /// Sets the fitness function for evaluating retry outcomes.
    /// </summary>
    /// <returns></returns>
    public EvolutionaryRetryPolicyBuilder<TContext> WithFitnessFunction(ToolCallMutationFitness fitnessFunction)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunction);
        _fitnessFunction = fitnessFunction;
        return this;
    }

    /// <summary>
    /// Builds the evolutionary retry policy.
    /// </summary>
    /// <returns></returns>
    public EvolutionaryRetryPolicy<TContext> Build()
    {
        return new EvolutionaryRetryPolicy<TContext>(
            _strategies, _maxGenerations, _logger, _chromosome, _fitnessFunction);
    }
}

/// <summary>
/// Static factory for creating pre-configured evolutionary retry policy builders.
/// </summary>
public static class EvolutionaryRetryPolicyBuilder
{
    /// <summary>
    /// Creates a builder pre-configured for tool call retries with all default mutation strategies.
    /// </summary>
    /// <returns></returns>
    public static EvolutionaryRetryPolicyBuilder<ToolCallContext> ForToolCalls()
    {
        return new EvolutionaryRetryPolicyBuilder<ToolCallContext>();
    }

    /// <summary>
    /// Creates a builder for tool call retries with all default strategies pre-registered.
    /// </summary>
    /// <returns></returns>
    public static EvolutionaryRetryPolicyBuilder<ToolCallContext> ForToolCallsWithDefaults()
    {
        return new EvolutionaryRetryPolicyBuilder<ToolCallContext>()
            .WithStrategy(new FormatHintMutation())
            .WithStrategy(new FormatSwitchMutation())
            .WithStrategy(new ToolSimplificationMutation())
            .WithStrategy(new TemperatureMutation());
    }

    /// <summary>
    /// Creates a builder with all default strategies plus GA chromosome and fitness function.
    /// This is the recommended configuration for full evolutionary retry support.
    /// </summary>
    /// <returns></returns>
    public static EvolutionaryRetryPolicyBuilder<ToolCallContext> ForToolCallsWithEvolution()
    {
        return new EvolutionaryRetryPolicyBuilder<ToolCallContext>()
            .WithStrategy(new FormatHintMutation())
            .WithStrategy(new FormatSwitchMutation())
            .WithStrategy(new ToolSimplificationMutation())
            .WithStrategy(new TemperatureMutation())
            .WithChromosome(ToolCallMutationChromosome.CreateDefault())
            .WithFitnessFunction(new ToolCallMutationFitness());
    }
}
