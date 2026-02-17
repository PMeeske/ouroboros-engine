// <copyright file="OrchestratorComposition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Orchestrator Composition Helpers
// Fluent API for composing and chaining orchestrators
// ==========================================================

namespace Ouroboros.Agent;

/// <summary>
/// Composite orchestrator that chains multiple orchestrators together.
/// Implements the Chain of Responsibility pattern for orchestrators.
/// </summary>
/// <typeparam name="TInput">Input type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public sealed class CompositeOrchestrator<TInput, TOutput> : OrchestratorBase<TInput, TOutput>, IComposableOrchestrator<TInput, TOutput>
{
    private readonly Func<TInput, OrchestratorContext, Task<TOutput>> _executeFunc;

    private CompositeOrchestrator(
        string name,
        OrchestratorConfig config,
        Func<TInput, OrchestratorContext, Task<TOutput>> executeFunc)
        : base(name, config)
    {
        _executeFunc = executeFunc ?? throw new ArgumentNullException(nameof(executeFunc));
    }

    /// <summary>
    /// Creates a composite orchestrator from a single base orchestrator.
    /// </summary>
    public static CompositeOrchestrator<TInput, TOutput> From(
        IOrchestrator<TInput, TOutput> orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        return new CompositeOrchestrator<TInput, TOutput>(
            orchestrator.Configuration.GetSetting("name", "composite_orchestrator") ?? "composite",
            orchestrator.Configuration,
            async (input, context) =>
            {
                var result = await orchestrator.ExecuteAsync(input, context);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Orchestration failed");
                }
                return result.Output!;
            });
    }

    /// <summary>
    /// Creates a composite orchestrator from a function.
    /// </summary>
    public static CompositeOrchestrator<TInput, TOutput> FromFunc(
        string name,
        Func<TInput, OrchestratorContext, Task<TOutput>> func,
        OrchestratorConfig? config = null)
    {
        return new CompositeOrchestrator<TInput, TOutput>(
            name,
            config ?? OrchestratorConfig.Default(),
            func);
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<TInput, TNext> Then<TNext>(
        IComposableOrchestrator<TOutput, TNext> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return new CompositeOrchestrator<TInput, TNext>(
            $"{OrchestratorName}_then_{next.Configuration.GetSetting<string>("name", "next")}",
            Configuration,
            async (input, context) =>
            {
                var intermediateResult = await _executeFunc(input, context);
                var finalResult = await next.ExecuteAsync(intermediateResult, context);
                if (!finalResult.Success)
                {
                    throw new InvalidOperationException(finalResult.ErrorMessage ?? "Chained orchestration failed");
                }
                return finalResult.Output!;
            });
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<TInput, TNext> Map<TNext>(
        Func<TOutput, TNext> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return new CompositeOrchestrator<TInput, TNext>(
            $"{OrchestratorName}_mapped",
            Configuration,
            async (input, context) =>
            {
                var intermediateResult = await _executeFunc(input, context);
                return mapper(intermediateResult);
            });
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<TInput, TOutput> Tap(
        Action<TOutput> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);

        return new CompositeOrchestrator<TInput, TOutput>(
            $"{OrchestratorName}_tapped",
            Configuration,
            async (input, context) =>
            {
                var result = await _executeFunc(input, context);
                sideEffect(result);
                return result;
            });
    }

    /// <inheritdoc/>
    protected override async Task<TOutput> ExecuteCoreAsync(TInput input, OrchestratorContext context)
    {
        return await _executeFunc(input, context);
    }
}