// <copyright file="NanoAtomArrows.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Provides Kleisli arrows and pipeline steps for integrating NanoOuroborosAtoms
/// with the Ouroboros functional pipeline architecture.
/// Follows the ConsolidatedMindArrows pattern.
/// </summary>
public static class NanoAtomArrows
{
    /// <summary>
    /// Creates a reasoning arrow that uses the NanoAtomChain for nano-context processing.
    /// Routes through self-consuming atoms and consolidates into a pipeline result.
    /// </summary>
    /// <param name="nanoModel">The nano-context LLM model.</param>
    /// <param name="config">NanoAtom configuration.</param>
    /// <returns>A pipeline step for nano reasoning.</returns>
    public static Step<PipelineBranch, PipelineBranch> NanoReasoningArrow(
        Ouroboros.Abstractions.Core.IChatCompletionModel nanoModel,
        NanoAtomConfig config)
    {
        return async branch =>
        {
            // Extract the most recent prompt from the branch
            string prompt = ExtractPrompt(branch);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return branch;
            }

            var chain = new NanoAtomChain(nanoModel, config);
            var result = await chain.ExecuteAsync(prompt).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                ReasoningState state = new Draft(result.Value.Content);
                return branch.WithReasoning(state, prompt, null);
            }

            return branch;
        };
    }

    /// <summary>
    /// Creates a single NanoAtom digest step for pipeline composition.
    /// Processes one ThoughtFragment through the self-consuming cycle.
    /// </summary>
    /// <param name="nanoModel">The nano-context LLM model.</param>
    /// <param name="config">NanoAtom configuration.</param>
    /// <returns>A step that digests a fragment.</returns>
    public static Step<ThoughtFragment, DigestFragment> NanoDigestArrow(
        Ouroboros.Abstractions.Core.IChatCompletionModel nanoModel,
        NanoAtomConfig config)
    {
        return async fragment =>
        {
            using var atom = new NanoOuroborosAtom(nanoModel, config);
            var result = await atom.ProcessAsync(fragment).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                return result.Value;
            }

            // Return a low-confidence digest on failure
            return new DigestFragment(
                Id: Guid.NewGuid(),
                SourceAtomId: atom.AtomId,
                Content: fragment.Content,
                CompressionRatio: 1.0,
                Confidence: 0.1,
                CompletedPhase: NanoAtomPhase.Process,
                Timestamp: DateTime.UtcNow);
        };
    }

    /// <summary>
    /// Creates a consolidation step that merges digest fragments into a ConsolidatedAction.
    /// </summary>
    /// <param name="nanoModel">The nano-context LLM model for synthesis.</param>
    /// <param name="config">NanoAtom configuration.</param>
    /// <returns>A step that consolidates digests.</returns>
    public static Step<IReadOnlyList<DigestFragment>, ConsolidatedAction> NanoConsolidateArrow(
        Ouroboros.Abstractions.Core.IChatCompletionModel nanoModel,
        NanoAtomConfig config)
    {
        return async digests =>
        {
            var consolidator = new ThoughtConsolidator(config, nanoModel);
            consolidator.AddDigests(digests);

            var result = await consolidator.ConsolidateAsync(streamCount: 1).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                return result.Value;
            }

            // Return a minimal action on failure
            return new ConsolidatedAction(
                Id: Guid.NewGuid(),
                Content: string.Join("\n", digests.Select(d => d.Content)),
                SourceDigests: digests,
                Confidence: 0.1,
                ActionType: "response",
                StreamCount: 1,
                ElapsedMs: 0,
                Timestamp: DateTime.UtcNow);
        };
    }

    /// <summary>
    /// Creates a Result-safe reasoning arrow with error handling.
    /// Follows the SafeIntelligentReasoningArrow pattern from ConsolidatedMindArrows.
    /// </summary>
    /// <param name="nanoModel">The nano-context LLM model.</param>
    /// <param name="config">NanoAtom configuration.</param>
    /// <returns>A Result-based pipeline step.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeNanoReasoningArrow(
        Ouroboros.Abstractions.Core.IChatCompletionModel nanoModel,
        NanoAtomConfig config)
    {
        return async branch =>
        {
            try
            {
                var result = await NanoReasoningArrow(nanoModel, config)(branch).ConfigureAwait(false);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<PipelineBranch, string>.Failure($"Nano reasoning failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Extracts the most recent prompt from a PipelineBranch's reasoning events.
    /// </summary>
    private static string ExtractPrompt(PipelineBranch branch)
    {
        var recentStep = branch.Events
            .OfType<ReasoningStep>()
            .LastOrDefault();

        return recentStep?.Prompt ?? string.Empty;
    }
}
