// <copyright file="NanoAtomChain.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Orchestrates the full NanoOuroborosAtom pipeline:
/// Fragment → Stream (parallel) → Process+Digest → Consolidate → Action.
///
/// Follows the DivideAndConquerOrchestrator parallel-process-merge pattern
/// but adds self-consuming digest steps and reactive streaming.
/// </summary>
public sealed class NanoAtomChain
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _model;
    private readonly NanoAtomConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoAtomChain"/> class.
    /// </summary>
    /// <param name="model">The nano-context LLM model for all atoms.</param>
    /// <param name="config">Configuration for token budgets and behavior.</param>
    public NanoAtomChain(
        Ouroboros.Abstractions.Core.IChatCompletionModel model,
        NanoAtomConfig config)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>
    /// Executes the full nano pipeline: prompt → fragments → parallel atoms → digests → action.
    /// </summary>
    /// <param name="prompt">The user prompt to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the ConsolidatedAction or an error.</returns>
    public async Task<Result<ConsolidatedAction, string>> ExecuteAsync(
        string prompt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        // Step 1: FRAGMENT — split prompt into ThoughtFragments
        var fragmenter = new ThoughtFragmenter(
            _config,
            _config.UseGoalDecomposer ? _model : null);

        ThoughtFragment[] fragments = await fragmenter.FragmentAsync(prompt, ct);

        if (fragments.Length == 0)
        {
            return Result<ConsolidatedAction, string>.Failure("No fragments produced from prompt");
        }

        // Limit parallel streams to config maximum
        int streamCount = Math.Min(fragments.Length, _config.MaxParallelAtoms);

        // Step 2: STREAM — create parallel ThoughtStreams with NanoAtoms
        var streams = new List<ThoughtStream>(streamCount);
        try
        {
            for (int i = 0; i < streamCount; i++)
            {
                var atom = new NanoOuroborosAtom(_model, _config);
                var stream = new ThoughtStream(atom);
                streams.Add(stream);
                stream.Start();
            }

            // Distribute fragments across streams (round-robin)
            for (int i = 0; i < fragments.Length; i++)
            {
                int streamIndex = i % streamCount;
                await streams[streamIndex].WriteAsync(fragments[i], ct);
            }

            // Signal completion on all streams
            foreach (var stream in streams)
            {
                stream.Complete();
            }

            // Step 3: COLLECT — gather all digests from all streams
            var consolidator = new ThoughtConsolidator(_config, _model);
            var collectTasks = streams.Select(s => s.CollectDigestsAsync(ct)).ToArray();
            var allDigests = await Task.WhenAll(collectTasks);

            foreach (var digestList in allDigests)
            {
                consolidator.AddDigests(digestList);
            }

            if (consolidator.DigestCount == 0)
            {
                return Result<ConsolidatedAction, string>.Failure(
                    "No digests produced — all atoms may have failed (circuit breakers open?)");
            }

            // Step 4: CONSOLIDATE — merge digests into a single ConsolidatedAction
            return await consolidator.ConsolidateAsync(streamCount, ct);
        }
        finally
        {
            // Cleanup all streams
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
