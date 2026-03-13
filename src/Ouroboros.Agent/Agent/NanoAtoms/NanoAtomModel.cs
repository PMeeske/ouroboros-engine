// <copyright file="NanoAtomModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using Ouroboros.Core.Monads;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// An <see cref="Ouroboros.Abstractions.Core.IChatCompletionModel"/> decorator that wraps
/// the entire NanoOuroborosAtom pipeline. Any prompt sent to this model is automatically
/// fragmented, processed through parallel nano-atoms with self-consuming digest steps,
/// and consolidated into a single response.
///
/// This allows the NanoAtom pipeline to be used as a transparent drop-in replacement
/// anywhere an <see cref="Ouroboros.Abstractions.Core.IChatCompletionModel"/> is expected —
/// including as a specialist within <see cref="ConsolidatedMind.ConsolidatedMind"/>,
/// in Kleisli arrow pipelines, or as a standalone model.
///
/// <code>
/// // Use as a standalone model
/// IChatCompletionModel model = new NanoAtomModel(tinyOllamaModel);
/// string response = await model.GenerateTextAsync("Explain quantum computing");
///
/// // Register as a specialist in ConsolidatedMind
/// var mind = new ConsolidatedMind();
/// mind.RegisterSpecialist(new SpecializedModel(
///     SpecializedRole.DeepReasoning, nanoAtomModel, "nano-atom", ...));
/// </code>
/// </summary>
public sealed class NanoAtomModel : Ouroboros.Abstractions.Core.IChatCompletionModel, IDisposable
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _innerModel;
    private readonly NanoAtomConfig _config;
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel? _synthesisModel;
    private long _totalRequests;
    private long _totalLatencyMs;
    private long _failedRequests;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoAtomModel"/> class.
    /// </summary>
    /// <param name="innerModel">The underlying nano-context model (e.g., qwen2.5:0.5b, tinyllama).</param>
    /// <param name="config">Configuration for NanoAtom behavior. Defaults to <see cref="NanoAtomConfig.Default"/>.</param>
    /// <param name="synthesisModel">Optional separate model for consolidation synthesis. If null, uses innerModel.</param>
    public NanoAtomModel(
        Ouroboros.Abstractions.Core.IChatCompletionModel innerModel,
        NanoAtomConfig? config = null,
        Ouroboros.Abstractions.Core.IChatCompletionModel? synthesisModel = null)
    {
        ArgumentNullException.ThrowIfNull(innerModel);
        _innerModel = innerModel;
        _config = config ?? NanoAtomConfig.Default();
        _synthesisModel = synthesisModel;
    }

    /// <summary>Gets the total number of requests processed.</summary>
    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    /// <summary>Gets the average latency in milliseconds.</summary>
    public double AverageLatencyMs => _totalRequests > 0
        ? (double)Interlocked.Read(ref _totalLatencyMs) / Interlocked.Read(ref _totalRequests)
        : 0;

    /// <summary>Gets the success rate as a value between 0.0 and 1.0.</summary>
    public double SuccessRate => _totalRequests > 0
        ? 1.0 - ((double)Interlocked.Read(ref _failedRequests) / Interlocked.Read(ref _totalRequests))
        : 1.0;

    /// <summary>Gets the last <see cref="ConsolidatedAction"/> produced by this model.</summary>
    public ConsolidatedAction? LastAction { get; private set; }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var stopwatch = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalRequests);

        try
        {
            var chain = new NanoAtomChain(_innerModel, _config);
            Result<ConsolidatedAction, string> result = await chain.ExecuteAsync(prompt, ct);

            stopwatch.Stop();
            Interlocked.Add(ref _totalLatencyMs, stopwatch.ElapsedMilliseconds);

            if (result.IsSuccess)
            {
                LastAction = result.Value;
                return result.Value.Content;
            }

            // Pipeline failed — fall back to direct model call
            Interlocked.Increment(ref _failedRequests);
            return await _innerModel.GenerateTextAsync(prompt, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            Interlocked.Add(ref _totalLatencyMs, stopwatch.ElapsedMilliseconds);
            Interlocked.Increment(ref _failedRequests);

            // Graceful degradation: fall back to direct model call
            return await _innerModel.GenerateTextAsync(prompt, ct);
        }
    }

    /// <summary>
    /// Processes a prompt and returns the full <see cref="ConsolidatedAction"/> with metadata,
    /// rather than just the string content.
    /// </summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full ConsolidatedAction with digests, confidence, and stream count.</returns>
    public async Task<Result<ConsolidatedAction, string>> ProcessFullAsync(
        string prompt,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result<ConsolidatedAction, string>.Failure("Prompt cannot be empty");
        }

        var chain = new NanoAtomChain(_innerModel, _config);
        var result = await chain.ExecuteAsync(prompt, ct);

        if (result.IsSuccess)
        {
            LastAction = result.Value;
        }

        return result;
    }

    /// <summary>
    /// Creates a <see cref="NanoAtomModel"/> with the <see cref="NanoAtomConfig.Minimal"/> preset.
    /// Best for the smallest models (qwen2.5:0.5b, tinyllama — 0.5b params).
    /// </summary>
    public static NanoAtomModel CreateMinimal(Ouroboros.Abstractions.Core.IChatCompletionModel innerModel)
        => new(innerModel, NanoAtomConfig.Minimal());

    /// <summary>
    /// Creates a <see cref="NanoAtomModel"/> with the <see cref="NanoAtomConfig.Default"/> preset.
    /// Balanced for models in the 1-3b param range (phi3:mini, stablelm2).
    /// </summary>
    public static NanoAtomModel CreateDefault(Ouroboros.Abstractions.Core.IChatCompletionModel innerModel)
        => new(innerModel, NanoAtomConfig.Default());

    /// <summary>
    /// Creates a <see cref="NanoAtomModel"/> with the <see cref="NanoAtomConfig.HighQuality"/> preset.
    /// For larger nano-context models (3-7b params) or when quality matters more than speed.
    /// </summary>
    public static NanoAtomModel CreateHighQuality(Ouroboros.Abstractions.Core.IChatCompletionModel innerModel)
        => new(innerModel, NanoAtomConfig.HighQuality());

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_innerModel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_synthesisModel is IDisposable synthDisposable)
        {
            synthDisposable.Dispose();
        }
    }
}
