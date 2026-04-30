// Copyright (c) Ouroboros. All rights reserved.

// ==========================================================
// Common Ground Tracker
// Clark (1996) Common Ground theory — tracks shared
// propositions, grounding methods, and misunderstanding
// detection per conversation partner.
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Social;

/// <summary>
/// A proposition grounded in the common ground with a specific person.
/// </summary>
/// <param name="Proposition">The proposition text.</param>
/// <param name="Method">How the proposition was grounded.</param>
/// <param name="Timestamp">When the proposition was added.</param>
/// <param name="Confidence">Confidence in shared understanding (0–1).</param>
internal sealed record GroundedProposition(
    string Proposition,
    GroundingMethod Method,
    DateTime Timestamp,
    double Confidence);

/// <summary>
/// Implements intersubjectivity tracking based on Clark's (1996) Common Ground theory.
/// Maintains per-person proposition sets with grounding method tracking, confidence
/// decay, and misunderstanding detection via keyword overlap heuristics.
/// </summary>
public sealed class CommonGroundTracker : IIntersubjectivity
{
    private const int MaxPropositionsPerPerson = 500;
    private const double DefaultConfidence = 0.8;
    private const double MisunderstandingThreshold = 0.4;

    private readonly ConcurrentDictionary<string, List<GroundedProposition>> _commonGround = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void AddToCommonGround(string personId, string proposition, GroundingMethod method)
    {
        ArgumentNullException.ThrowIfNull(personId);
        ArgumentNullException.ThrowIfNull(proposition);

        double confidence = method switch
        {
            GroundingMethod.Explicit => 0.95,
            GroundingMethod.Implicit => 0.70,
            GroundingMethod.Presupposed => 0.60,
            GroundingMethod.Inferred => 0.50,
            _ => DefaultConfidence,
        };

        var grounded = new GroundedProposition(proposition, method, DateTime.UtcNow, confidence);

        _commonGround.AddOrUpdate(
            personId,
            _ => [grounded],
            (_, existing) =>
            {
                lock (_lock)
                {
                    // Avoid duplicates by proposition text
                    if (!existing.Any(p => string.Equals(
                            p.Proposition, proposition, StringComparison.OrdinalIgnoreCase)))
                    {
                        existing.Add(grounded);
                        EnforceCapacity(existing);
                    }

                    return existing;
                }
            });
    }

    /// <inheritdoc />
    public bool IsInCommonGround(string personId, string proposition)
    {
        ArgumentNullException.ThrowIfNull(personId);
        ArgumentNullException.ThrowIfNull(proposition);

        if (!_commonGround.TryGetValue(personId, out var propositions))
            return false;

        lock (_lock)
        {
            return propositions.Any(p =>
                p.Proposition.Contains(proposition, StringComparison.OrdinalIgnoreCase) ||
                proposition.Contains(p.Proposition, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc />
    public List<string> GetCommonGround(string personId)
    {
        ArgumentNullException.ThrowIfNull(personId);

        if (!_commonGround.TryGetValue(personId, out var propositions))
            return [];

        lock (_lock)
        {
            return propositions
                .OrderByDescending(p => p.Confidence)
                .Select(p => p.Proposition)
                .ToList();
        }
    }

    /// <inheritdoc />
    public Task<Result<MisunderstandingDetection, string>> DetectMisunderstandingAsync(
        string personId, string utterance, string response, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(personId);
        ArgumentNullException.ThrowIfNull(utterance);
        ArgumentNullException.ThrowIfNull(response);
        ct.ThrowIfCancellationRequested();

        var utteranceWords = ExtractKeywords(utterance);
        var responseWords = ExtractKeywords(response);

        // Measure semantic overlap between utterance and response
        int overlap = utteranceWords.Intersect(responseWords, StringComparer.OrdinalIgnoreCase).Count();
        int totalUnique = utteranceWords.Union(responseWords, StringComparer.OrdinalIgnoreCase).Count();
        double coherence = totalUnique > 0 ? (double)overlap / totalUnique : 0.0;

        // Check against common ground for additional signals
        var commonGroundProps = GetCommonGround(personId);
        bool contradicts = commonGroundProps.Any(p =>
            responseWords.Any(w => p.Contains(w, StringComparison.OrdinalIgnoreCase)) &&
            coherence < 0.3);

        bool misunderstandingDetected = coherence < MisunderstandingThreshold || contradicts;
        double confidence = Math.Clamp(1.0 - coherence, 0.0, 1.0);

        string misaligned = misunderstandingDetected
            ? $"Response diverges from utterance topic ({coherence:F2} coherence)"
            : string.Empty;

        string clarification = misunderstandingDetected
            ? $"Consider clarifying: the utterance focused on '{Truncate(utterance, 60)}' " +
              $"but the response addressed '{Truncate(response, 60)}'"
            : string.Empty;

        var detection = new MisunderstandingDetection(
            misunderstandingDetected,
            misaligned,
            clarification,
            Math.Round(confidence, 3));

        return Task.FromResult(Result<MisunderstandingDetection, string>.Success(detection));
    }

    /// <inheritdoc />
    public void RecordGroundingSuccess(string personId, string proposition, bool understood)
    {
        ArgumentNullException.ThrowIfNull(personId);
        ArgumentNullException.ThrowIfNull(proposition);

        if (!_commonGround.TryGetValue(personId, out var propositions))
            return;

        lock (_lock)
        {
            for (int i = 0; i < propositions.Count; i++)
            {
                if (!string.Equals(
                        propositions[i].Proposition, proposition, StringComparison.OrdinalIgnoreCase))
                    continue;

                double adjustment = understood ? 0.1 : -0.2;
                double newConfidence = Math.Clamp(propositions[i].Confidence + adjustment, 0.0, 1.0);
                propositions[i] = propositions[i] with { Confidence = newConfidence };
                break;
            }
        }
    }

    /// <summary>
    /// Returns the number of unique conversation partners being tracked.
    /// </summary>
    public int TrackedPartnerCount => _commonGround.Count;

    private static HashSet<string> ExtractKeywords(string text)
    {
        var words = text.Split(
            [' ', ',', '.', ';', ':', '-', '_', '/', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. words.Where(w => w.Length > 2).Select(w => w.ToLowerInvariant())];
    }

    private static string Truncate(string text, int maxLen = 60)
    {
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }

    private static void EnforceCapacity(List<GroundedProposition> propositions)
    {
        if (propositions.Count <= MaxPropositionsPerPerson)
            return;

        // Remove oldest, lowest-confidence entries
        var toPrune = propositions
            .OrderBy(p => p.Confidence)
            .ThenBy(p => p.Timestamp)
            .Take(propositions.Count - MaxPropositionsPerPerson + 10)
            .ToHashSet();

        propositions.RemoveAll(p => toPrune.Contains(p));
    }
}
