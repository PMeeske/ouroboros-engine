// <copyright file="EvolutionaryRetryPolicyBuilder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Fluent builder for constructing <see cref="EvolutionaryRetryPolicy{TContext}"/> instances.
/// </summary>
public sealed class EvolutionaryRetryPolicyBuilder<TContext> where TContext : class
{
    private readonly List<IMutationStrategy<TContext>> _strategies = [];
    private int _maxGenerations = 5;
    private ILogger? _logger;

    /// <summary>
    /// Adds a mutation strategy to the policy.
    /// </summary>
    public EvolutionaryRetryPolicyBuilder<TContext> WithStrategy(IMutationStrategy<TContext> strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategies.Add(strategy);
        return this;
    }

    /// <summary>
    /// Sets the maximum number of evolutionary generations.
    /// </summary>
    public EvolutionaryRetryPolicyBuilder<TContext> WithMaxGenerations(int maxGenerations)
    {
        _maxGenerations = maxGenerations;
        return this;
    }

    /// <summary>
    /// Sets the logger for the policy.
    /// </summary>
    public EvolutionaryRetryPolicyBuilder<TContext> WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Builds the evolutionary retry policy.
    /// </summary>
    public EvolutionaryRetryPolicy<TContext> Build()
    {
        return new EvolutionaryRetryPolicy<TContext>(_strategies, _maxGenerations, _logger);
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
    public static EvolutionaryRetryPolicyBuilder<ToolCallContext> ForToolCalls()
    {
        return new EvolutionaryRetryPolicyBuilder<ToolCallContext>();
    }

    /// <summary>
    /// Creates a builder for tool call retries with all default strategies pre-registered.
    /// </summary>
    public static EvolutionaryRetryPolicyBuilder<ToolCallContext> ForToolCallsWithDefaults()
    {
        return new EvolutionaryRetryPolicyBuilder<ToolCallContext>()
            .WithStrategy(new FormatHintMutation())
            .WithStrategy(new FormatSwitchMutation())
            .WithStrategy(new ToolSimplificationMutation())
            .WithStrategy(new TemperatureMutation());
    }
}
