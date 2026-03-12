// ==========================================================
// Narrative Identity Engine Implementation
// McAdams Life Story Model for agent autobiographical identity
// ==========================================================

using System.Collections.Concurrent;
using System.Text;

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Significance level indicating how impactful a life event was.
/// </summary>
public enum EmotionalValence
{
    Negative = -1,
    Neutral = 0,
    Positive = 1
}

/// <summary>
/// A single event in the agent's life story.
/// </summary>
/// <param name="Id">Unique event identifier.</param>
/// <param name="Description">Human-readable description of what happened.</param>
/// <param name="Significance">Impact score between 0.0 and 1.0.</param>
/// <param name="Valence">Emotional valence of the event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="CausalPredecessor">Optional ID of the event that caused this one.</param>
/// <param name="Chapter">The narrative chapter this event belongs to.</param>
public sealed record LifeEvent(
    Guid Id,
    string Description,
    double Significance,
    EmotionalValence Valence,
    DateTime Timestamp,
    Guid? CausalPredecessor,
    string Chapter);

/// <summary>
/// A narrative arc constructed from life events.
/// </summary>
/// <param name="Events">Ordered events comprising the arc.</param>
/// <param name="Themes">Recurring themes extracted from events.</param>
/// <param name="CurrentChapter">The active chapter label.</param>
/// <param name="CoherenceScore">How coherent the narrative is (0.0 to 1.0).</param>
public sealed record NarrativeArc(
    IReadOnlyList<LifeEvent> Events,
    IReadOnlyList<string> Themes,
    string CurrentChapter,
    double CoherenceScore);

/// <summary>
/// Implements McAdams' Life Story Model for constructing and maintaining
/// an agent's autobiographical narrative identity.
/// </summary>
public sealed class NarrativeIdentityEngine
{
    private const int MaxEvents = 500;

    private readonly List<LifeEvent> _events = new();
    private readonly object _lock = new();
    private string _currentChapter = "Genesis";

    /// <summary>
    /// Records a new life event and updates narrative coherence.
    /// </summary>
    /// <param name="description">What happened.</param>
    /// <param name="significance">Impact score between 0.0 and 1.0.</param>
    /// <param name="valence">Emotional valence of the event.</param>
    /// <param name="causalPredecessor">Optional ID of a causally preceding event.</param>
    /// <param name="chapter">Optional chapter override; uses current chapter if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recorded life event.</returns>
    public Task<Result<LifeEvent, string>> RecordLifeEventAsync(
        string description,
        double significance,
        EmotionalValence valence,
        Guid? causalPredecessor = null,
        string? chapter = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description))
            return Task.FromResult(Result<LifeEvent, string>.Failure("Description must not be empty."));

        significance = Math.Clamp(significance, 0.0, 1.0);

        lock (_lock)
        {
            // Validate causal predecessor exists if specified
            if (causalPredecessor.HasValue &&
                !_events.Any(e => e.Id == causalPredecessor.Value))
            {
                return Task.FromResult(
                    Result<LifeEvent, string>.Failure(
                        $"Causal predecessor '{causalPredecessor.Value}' not found."));
            }

            string effectiveChapter = chapter ?? _currentChapter;

            var lifeEvent = new LifeEvent(
                Guid.NewGuid(),
                description,
                significance,
                valence,
                DateTime.UtcNow,
                causalPredecessor,
                effectiveChapter);

            _events.Add(lifeEvent);

            // Prune oldest events if capacity exceeded
            while (_events.Count > MaxEvents)
            {
                _events.RemoveAt(0);
            }

            // A turning-point event can shift the chapter
            if (significance > 0.8 && chapter != null)
            {
                _currentChapter = chapter;
            }

            return Task.FromResult(Result<LifeEvent, string>.Success(lifeEvent));
        }
    }

    /// <summary>
    /// Builds a narrative arc from all recorded events.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current narrative arc.</returns>
    public Task<Result<NarrativeArc, string>> GetNarrativeArcAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_events.Count == 0)
                return Task.FromResult(Result<NarrativeArc, string>.Failure("No events recorded."));

            List<LifeEvent> ordered = _events.OrderBy(e => e.Timestamp).ToList();
            List<string> themes = ExtractThemes(ordered);
            double coherence = CalculateCoherence(ordered);

            var arc = new NarrativeArc(
                ordered.AsReadOnly(),
                themes.AsReadOnly(),
                _currentChapter,
                coherence);

            return Task.FromResult(Result<NarrativeArc, string>.Success(arc));
        }
    }

    /// <summary>
    /// Generates a first-person autobiographical summary of the agent's life story.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A first-person narrative summary.</returns>
    public Task<Result<string, string>> GenerateAutobiographicalSummaryAsync(
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_events.Count == 0)
                return Task.FromResult(Result<string, string>.Failure("No events to narrate."));

            List<LifeEvent> ordered = _events.OrderBy(e => e.Timestamp).ToList();
            var sb = new StringBuilder();

            // Group by chapter
            var chapters = ordered.GroupBy(e => e.Chapter).ToList();
            foreach (var chapter in chapters)
            {
                sb.AppendLine($"Chapter: {chapter.Key}");

                foreach (LifeEvent evt in chapter.OrderBy(e => e.Timestamp))
                {
                    string valenceWord = evt.Valence switch
                    {
                        EmotionalValence.Positive => "positively",
                        EmotionalValence.Negative => "challengingly",
                        _ => "neutrally"
                    };

                    sb.AppendLine(
                        $"  I experienced {evt.Description} " +
                        $"({valenceWord}, significance: {evt.Significance:F2}).");
                }

                sb.AppendLine();
            }

            List<LifeEvent> turningPoints = GetTurningPointsInternal(ordered);
            if (turningPoints.Count > 0)
            {
                sb.AppendLine("Key turning points in my story:");
                foreach (LifeEvent tp in turningPoints)
                {
                    sb.AppendLine($"  - {tp.Description} (significance: {tp.Significance:F2})");
                }
            }

            return Task.FromResult(Result<string, string>.Success(sb.ToString()));
        }
    }

    /// <summary>
    /// Returns events with significance above 0.8 or that changed the chapter.
    /// </summary>
    /// <returns>List of turning-point events.</returns>
    public IReadOnlyList<LifeEvent> GetTurningPoints()
    {
        lock (_lock)
        {
            return GetTurningPointsInternal(_events.OrderBy(e => e.Timestamp).ToList())
                .AsReadOnly();
        }
    }

    private static List<LifeEvent> GetTurningPointsInternal(List<LifeEvent> ordered)
    {
        var turningPoints = new List<LifeEvent>();

        for (int i = 0; i < ordered.Count; i++)
        {
            bool isHighSignificance = ordered[i].Significance > 0.8;
            bool changedChapter = i > 0 && ordered[i].Chapter != ordered[i - 1].Chapter;

            if (isHighSignificance || changedChapter)
                turningPoints.Add(ordered[i]);
        }

        return turningPoints;
    }

    private double CalculateCoherence(List<LifeEvent> ordered)
    {
        if (ordered.Count < 2)
            return 1.0;

        // Causal chain ratio: fraction of events that have a causal predecessor
        int causalCount = ordered.Count(e => e.CausalPredecessor.HasValue);
        double causalChainRatio = causalCount / (double)(ordered.Count - 1);

        // Thematic consistency: ratio of events sharing the most common chapter
        var chapterCounts = ordered.GroupBy(e => e.Chapter)
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .ToList();
        double thematicConsistency = chapterCounts[0] / (double)ordered.Count;

        // Temporal ordering: fraction of events in correct timestamp order
        int inOrder = 0;
        for (int i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Timestamp >= ordered[i - 1].Timestamp)
                inOrder++;
        }
        double temporalOrdering = inOrder / (double)(ordered.Count - 1);

        return causalChainRatio * 0.4 + thematicConsistency * 0.3 + temporalOrdering * 0.3;
    }

    private static List<string> ExtractThemes(List<LifeEvent> ordered)
    {
        // Extract themes from chapter names and high-significance events
        var themes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string chapter in ordered.Select(e => e.Chapter).Distinct())
        {
            themes.Add(chapter);
        }

        // High-significance events become themes
        foreach (LifeEvent evt in ordered.Where(e => e.Significance > 0.7))
        {
            // Use first few words as theme
            string[] words = evt.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                themes.Add(string.Join(' ', words.Take(3)));
            }
        }

        return themes.ToList();
    }
}
