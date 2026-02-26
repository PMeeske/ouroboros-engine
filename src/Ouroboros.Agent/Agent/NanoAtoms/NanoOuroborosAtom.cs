// <copyright file="NanoOuroborosAtom.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using Ouroboros.Core.Monads;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// A self-consuming atomic processor that operates within nano-context token budgets.
/// Each atom follows the ouroboros cycle: Receive → Process → Digest (self-consume) → Emit.
/// The digest step is the "ouroboros bite" — the atom re-processes its own output to compress it.
///
/// Integrates with:
/// - Circuit breaker pattern (from ResilientReasoner) for fault tolerance
/// - SelfCritique pattern for confidence scoring in the digest step
/// </summary>
public sealed class NanoOuroborosAtom : IDisposable
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _model;
    private readonly NanoAtomConfig _config;
    private readonly Guid _atomId = Guid.NewGuid();
    private int _consecutiveFailures;
    private bool _circuitOpen;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NanoOuroborosAtom"/> class.
    /// </summary>
    /// <param name="model">The nano-context LLM model (e.g., qwen2.5:0.5b, tinyllama).</param>
    /// <param name="config">Configuration for token budgets and behavior.</param>
    public NanoOuroborosAtom(
        Ouroboros.Abstractions.Core.IChatCompletionModel model,
        NanoAtomConfig config)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>Gets the unique identifier for this atom.</summary>
    public Guid AtomId => _atomId;

    /// <summary>Gets the current phase of the atom's cycle.</summary>
    public NanoAtomPhase CurrentPhase { get; private set; } = NanoAtomPhase.Idle;

    /// <summary>Gets whether the circuit breaker is currently open.</summary>
    public bool IsCircuitOpen => _circuitOpen;

    /// <summary>Gets the total number of fragments processed by this atom.</summary>
    public int FragmentsProcessed { get; private set; }

    /// <summary>
    /// Processes a ThoughtFragment through the full nano-ouroboros cycle:
    /// Receive → Process (LLM call) → Digest (self-consume/compress) → Emit.
    /// </summary>
    /// <param name="fragment">The thought fragment to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the compressed DigestFragment or an error.</returns>
    public async Task<Result<DigestFragment, string>> ProcessAsync(
        ThoughtFragment fragment,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(fragment);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Phase 1: RECEIVE — validate and truncate to token budget
            CurrentPhase = NanoAtomPhase.Receive;
            string input = TruncateToTokenBudget(fragment.Content, _config.MaxInputTokens);

            // Phase 2: PROCESS — LLM call within token budget
            CurrentPhase = NanoAtomPhase.Process;
            string rawOutput;

            if (_circuitOpen && _config.EnableCircuitBreaker)
            {
                // Circuit is open — use symbolic compression (fallback)
                rawOutput = SymbolicProcess(input);
            }
            else
            {
                try
                {
                    string prompt = string.Format(_config.ProcessPrompt, input);
                    rawOutput = await _model.GenerateTextAsync(prompt, ct);
                    ResetCircuitBreaker();
                }
                catch (Exception) when (_config.EnableCircuitBreaker)
                {
                    RecordFailure();
                    rawOutput = SymbolicProcess(input);
                }
            }

            // Phase 3: DIGEST — self-consuming ouroboros bite (compress own output)
            CurrentPhase = NanoAtomPhase.Digest;
            string digestContent;
            double confidence;

            if (_circuitOpen && _config.EnableCircuitBreaker)
            {
                // Symbolic digest when circuit is open
                digestContent = SymbolicDigest(rawOutput);
                confidence = 0.4; // Lower confidence for symbolic fallback
            }
            else
            {
                try
                {
                    string digestPrompt = string.Format(
                        _config.DigestPrompt,
                        _config.DigestTargetTokens,
                        rawOutput);
                    digestContent = await _model.GenerateTextAsync(digestPrompt, ct);
                    ResetCircuitBreaker();

                    // Confidence from mini self-critique (if enabled)
                    confidence = _config.EnableSelfCritique
                        ? EstimateConfidence(rawOutput, digestContent)
                        : 0.7; // Default medium confidence
                }
                catch (Exception) when (_config.EnableCircuitBreaker)
                {
                    RecordFailure();
                    digestContent = SymbolicDigest(rawOutput);
                    confidence = 0.4;
                }
            }

            // Phase 4: EMIT — produce DigestFragment
            CurrentPhase = NanoAtomPhase.Emit;

            int originalTokens = ThoughtFragment.EstimateTokenCount(fragment.Content);
            int digestTokens = ThoughtFragment.EstimateTokenCount(digestContent);
            double compressionRatio = digestTokens > 0
                ? (double)originalTokens / digestTokens
                : 1.0;

            var digest = new DigestFragment(
                Id: Guid.NewGuid(),
                SourceAtomId: _atomId,
                Content: digestContent,
                CompressionRatio: compressionRatio,
                Confidence: confidence,
                CompletedPhase: NanoAtomPhase.Emit,
                Timestamp: DateTime.UtcNow);

            FragmentsProcessed++;
            CurrentPhase = NanoAtomPhase.Idle;

            return Result<DigestFragment, string>.Success(digest);
        }
        catch (OperationCanceledException)
        {
            CurrentPhase = NanoAtomPhase.Idle;
            return Result<DigestFragment, string>.Failure("Processing was cancelled");
        }
        catch (Exception ex)
        {
            CurrentPhase = NanoAtomPhase.Idle;
            return Result<DigestFragment, string>.Failure($"NanoAtom processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Truncates text to fit within the token budget using BPE approximation.
    /// </summary>
    private static string TruncateToTokenBudget(string text, int maxTokens)
    {
        int maxChars = maxTokens * 4; // ~4 chars per token (BPE approximation)
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    /// <summary>
    /// Symbolic processing fallback when circuit breaker is open.
    /// Extracts key sentences as a rule-based response.
    /// </summary>
    private static string SymbolicProcess(string input)
    {
        // Extract first and last sentences as symbolic summary
        string[] sentences = input.Split(
            ['.', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return sentences.Length switch
        {
            0 => input,
            1 => sentences[0],
            _ => $"{sentences[0]}. {sentences[^1]}."
        };
    }

    /// <summary>
    /// Symbolic digest fallback — rule-based compression without LLM.
    /// Extracts the most information-dense portion of the text.
    /// </summary>
    private static string SymbolicDigest(string output)
    {
        // Take first ~25% of the text (most important info is usually first)
        int targetLength = Math.Max(50, output.Length / 4);
        if (output.Length <= targetLength)
        {
            return output;
        }

        // Cut at sentence boundary
        int cutPoint = output.LastIndexOf('.', targetLength);
        return cutPoint > 0 ? output[..(cutPoint + 1)] : output[..targetLength];
    }

    /// <summary>
    /// Estimates confidence using the SelfCritique pattern — checks if the digest
    /// preserves key terms from the original output (nano-scale quality metric).
    /// </summary>
    private static double EstimateConfidence(string original, string digest)
    {
        if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(digest))
        {
            return 0.3;
        }

        // Extract significant words (>= 4 chars) from original
        HashSet<string> originalWords = new(
            original.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 4)
                .Select(w => w.ToLowerInvariant().Trim('.', ',', '!', '?', ':')),
            StringComparer.OrdinalIgnoreCase);

        if (originalWords.Count == 0)
        {
            return 0.5;
        }

        // Count how many significant words are preserved in the digest
        int preserved = digest
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim('.', ',', '!', '?', ':'))
            .Count(w => originalWords.Contains(w));

        double retention = (double)preserved / originalWords.Count;

        // Map retention to confidence: 0.3-0.95 range
        return Math.Clamp(0.3 + (retention * 0.65), 0.3, 0.95);
    }

    private void RecordFailure()
    {
        _consecutiveFailures++;
        if (_config.EnableCircuitBreaker &&
            _consecutiveFailures >= _config.CircuitBreakerFailureThreshold)
        {
            _circuitOpen = true;
        }
    }

    private void ResetCircuitBreaker()
    {
        _consecutiveFailures = 0;
        _circuitOpen = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
    }
}
