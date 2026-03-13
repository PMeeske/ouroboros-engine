// <copyright file="NanoAtomOrchestrator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Orchestrator that processes prompts through the NanoOuroborosAtom pipeline.
/// Implements <see cref="IComposableOrchestrator{TInput, TOutput}"/> so it integrates
/// with the main agent interface and can be composed with other orchestrators via
/// <c>.Then()</c>, <c>.Map()</c>, and <c>.Tap()</c>.
///
/// Usage:
/// <code>
/// var pipeline = nanoOrchestrator
///     .Map(action => action.Content)
///     .Then(verificationOrchestrator)
///     .Tap(result => logger.Log(result));
/// </code>
/// </summary>
public sealed class NanoAtomOrchestrator
    : OrchestratorBase<string, ConsolidatedAction>,
      IComposableOrchestrator<string, ConsolidatedAction>
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _model;
    private readonly NanoAtomConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoAtomOrchestrator"/> class.
    /// </summary>
    /// <param name="model">The nano-context LLM model.</param>
    /// <param name="config">Configuration for NanoAtom behavior.</param>
    /// <param name="orchestratorConfig">Optional orchestrator-level configuration.</param>
    /// <param name="safetyGuard">Optional safety guard.</param>
    public NanoAtomOrchestrator(
        Ouroboros.Abstractions.Core.IChatCompletionModel model,
        NanoAtomConfig config,
        OrchestratorConfig? orchestratorConfig = null,
        ISafetyGuard? safetyGuard = null)
        : base("NanoAtomOrchestrator", orchestratorConfig ?? OrchestratorConfig.Default(), safetyGuard)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <inheritdoc/>
    protected override async Task<ConsolidatedAction> ExecuteCoreAsync(
        string input,
        OrchestratorContext context)
    {
        var chain = new NanoAtomChain(_model, _config);
        var result = await chain.ExecuteAsync(input, context.CancellationToken);

        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw new InvalidOperationException(result.Error);
    }

    /// <inheritdoc/>
    protected override Result<bool, string> ValidateInput(string input, OrchestratorContext context)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<bool, string>.Failure("Prompt cannot be empty");
        }

        return Result<bool, string>.Success(true);
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<string, TNext> Then<TNext>(
        IComposableOrchestrator<ConsolidatedAction, TNext> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return CompositeOrchestrator<string, TNext>.FromFunc(
            $"NanoAtomOrchestrator_then_{next.Configuration.GetSetting<string>("name", "next")}",
            async (input, context) =>
            {
                var myResult = await ExecuteAsync(input, context);
                if (!myResult.Success)
                {
                    throw new InvalidOperationException(myResult.ErrorMessage ?? "NanoAtom orchestration failed");
                }

                var nextResult = await next.ExecuteAsync(myResult.Output!, context);
                if (!nextResult.Success)
                {
                    throw new InvalidOperationException(nextResult.ErrorMessage ?? "Chained orchestration failed");
                }

                return nextResult.Output!;
            },
            Configuration);
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<string, TNext> Map<TNext>(
        Func<ConsolidatedAction, TNext> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return CompositeOrchestrator<string, TNext>.FromFunc(
            "NanoAtomOrchestrator_mapped",
            async (input, context) =>
            {
                var result = await ExecuteAsync(input, context);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "NanoAtom orchestration failed");
                }

                return mapper(result.Output!);
            },
            Configuration);
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<string, ConsolidatedAction> Tap(
        Action<ConsolidatedAction> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);

        return CompositeOrchestrator<string, ConsolidatedAction>.FromFunc(
            "NanoAtomOrchestrator_tapped",
            async (input, context) =>
            {
                var result = await ExecuteAsync(input, context);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "NanoAtom orchestration failed");
                }

                sideEffect(result.Output!);
                return result.Output!;
            },
            Configuration);
    }

    /// <inheritdoc/>
    protected override Task<Dictionary<string, object>> GetCustomHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(new Dictionary<string, object>
        {
            ["max_input_tokens"] = _config.MaxInputTokens,
            ["max_output_tokens"] = _config.MaxOutputTokens,
            ["max_parallel_atoms"] = _config.MaxParallelAtoms,
            ["circuit_breaker_enabled"] = _config.EnableCircuitBreaker,
            ["self_critique_enabled"] = _config.EnableSelfCritique,
            ["goal_decomposer_enabled"] = _config.UseGoalDecomposer,
        });
    }
}
