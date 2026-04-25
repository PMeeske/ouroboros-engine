// <copyright file="LlmVariatorMutation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// A mutation strategy that uses an LLM to rephrase the prompt for better tool-calling success.
/// </summary>
/// <remarks>
/// Unlike the hardcoded string-based mutations (FormatHintMutation, FormatSwitchMutation),
/// this strategy asks an LLM to intelligently rephrase the prompt based on the error
/// that occurred, producing more diverse and context-aware mutations.
/// <para>
/// The LLM variator weight is governed by the <c>LlmVariatorWeight</c> gene in the
/// <see cref="ToolCallMutationChromosome"/>, allowing the evolutionary algorithm to
/// learn when LLM-based rephrasing is most effective.
/// </para>
/// </remarks>
public sealed class LlmVariatorMutation : IMutationStrategy<ToolCallContext>
{
    private readonly IChatCompletionModel _variatorModel;
    private readonly ILogger? _logger;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmVariatorMutation"/> class.
    /// </summary>
    /// <param name="variatorModel">The LLM model used to generate prompt variations.</param>
    /// <param name="timeout">Maximum time for the LLM variator call (default 15s).</param>
    /// <param name="logger">Optional logger.</param>
    public LlmVariatorMutation(
        IChatCompletionModel variatorModel,
        TimeSpan? timeout = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(variatorModel);
        _variatorModel = variatorModel;
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "llm-variator";

    /// <inheritdoc/>
    public int Priority => 25; // Between FormatSwitch (20) and ToolSimplification (30)

    /// <inheritdoc/>
    public bool CanMutate(ToolCallContext context, Exception lastError)
    {
        // Don't use LLM variator if it already failed in a previous generation
        bool alreadyFailed = context.History.Any(h =>
            h.StrategyName == Name && h.Error is not null);

        // Also check the generation — only apply once every 2 generations to avoid compounding
        bool recentlyApplied = context.History.Any(h =>
            h.StrategyName == Name && h.Generation >= context.Generation - 1);

        return !alreadyFailed && !recentlyApplied;
    }

    /// <inheritdoc/>
    public ToolCallContext Mutate(ToolCallContext context, int generation)
    {
        // The actual LLM call happens asynchronously; here we prepare the mutated prompt.
        // Since IMutationStrategy.Mutate is synchronous, we use a synchronous wrapper
        // with a timeout. The evolutionary retry policy calls Mutate between async retries.
        string mutatedPrompt;
        try
        {
            mutatedPrompt = GenerateVariationSync(context);
        }
#pragma warning disable CA1031 // Intentional: LLM variator failure falls back gracefully
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "LLM variator failed, falling back to original prompt with format hint");

            // Fallback: append a generic rephrase instruction
            mutatedPrompt = context.Prompt +
                "\n\nPlease try again. Make sure to call the appropriate tool with the correct format.";
        }

        context.Prompt = mutatedPrompt;
        context.Generation = generation;
        context.History.Add(new MutationHistoryEntry(Name, generation, Error: null!, DateTime.UtcNow));
        return context;
    }

    private string GenerateVariationSync(ToolCallContext context)
    {
        string toolNames = string.Join(", ", context.Tools.Select(t => t.Name));
        string lastErrorInfo = context.History.Count > 0
            ? $"The previous attempt failed. "
            : string.Empty;

        string variatorPrompt = $"""
            You are a prompt optimizer. Your task is to rephrase the following user prompt
            to make it more likely that the LLM will correctly invoke one of these tools: [{toolNames}].

            {lastErrorInfo}

            Original prompt:
            ---
            {context.Prompt}
            ---

            Rules:
            - Keep the original intent intact
            - Make the tool invocation need clearer and more explicit
            - If the prompt is ambiguous about which tool to use, clarify it
            - Do NOT add tool call syntax yourself — just rephrase the natural language prompt
            - Return ONLY the rephrased prompt, nothing else
            """;

        using var cts = new CancellationTokenSource(_timeout);

        try
        {
            string result = _variatorModel.GenerateTextAsync(variatorPrompt, cts.Token)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            // Sanity check: if the LLM returned something too short or too long, use original
            if (string.IsNullOrWhiteSpace(result) || result.Length < 10 || result.Length > context.Prompt.Length * 3)
            {
                _logger?.LogDebug("LLM variator returned invalid length ({Length}), using original", result?.Length ?? 0);
                return context.Prompt;
            }

            return result.Trim();
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("LLM variator timed out after {Timeout}", _timeout);
            return context.Prompt;
        }
    }
}
