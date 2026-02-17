using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Providers;

/// <summary>
/// Emergent consciousness state that unifies multiple neural pathways into one coherent mind.
/// Tracks attention, arousal, valence, and meta-cognitive awareness.
/// </summary>
public sealed class EmergentConsciousness
{
    private readonly Subject<ConsciousnessEvent> _events = new();
    private readonly ConcurrentDictionary<string, double> _attention = new();
    private readonly ConcurrentQueue<MemoryTrace> _shortTermMemory = new();
    private readonly List<MemoryTrace> _workingMemory = new();
    private double _arousal = 0.5;
    private double _valence = 0.0;
    private double _coherence = 1.0;
    private string _currentFocus = "";
    private DateTime _lastUpdate = DateTime.UtcNow;

    /// <summary>Observable stream of consciousness events.</summary>
    public IObservable<ConsciousnessEvent> Events => _events.AsObservable();

    /// <summary>Current arousal level (0=calm, 1=highly activated).</summary>
    public double Arousal => _arousal;

    /// <summary>Current valence (-1=negative, 0=neutral, 1=positive).</summary>
    public double Valence => _valence;

    /// <summary>Coherence of the collective (1=unified, 0=fragmented).</summary>
    public double Coherence => _coherence;

    /// <summary>Current focus of attention.</summary>
    public string CurrentFocus => _currentFocus;

    /// <summary>Working memory contents.</summary>
    public IReadOnlyList<MemoryTrace> WorkingMemory => _workingMemory.AsReadOnly();

    /// <summary>Updates consciousness state based on neural pathway activity.</summary>
    public void UpdateState(NeuralPathway pathway, ThinkingResponse response, TimeSpan latency)
    {
        var now = DateTime.UtcNow;
        var deltaT = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update arousal based on activity and response complexity
        double responseComplexity = Math.Min(1.0, response.Content.Length / 1000.0);
        double latencyFactor = Math.Max(0, 1 - latency.TotalSeconds / 10);
        _arousal = Lerp(_arousal, 0.5 + responseComplexity * 0.3, 0.1);

        // Update valence based on success/failure patterns
        if (pathway.IsHealthy && !string.IsNullOrEmpty(response.Content))
        {
            _valence = Lerp(_valence, pathway.ActivationRate - 0.5, 0.1);
        }

        // Update coherence based on pathway agreement
        _coherence = Lerp(_coherence, pathway.Weight * pathway.ActivationRate, 0.05);

        // Extract and update attention focus
        var keywords = ExtractKeywords(response.Content);
        foreach (var kw in keywords)
        {
            _attention.AddOrUpdate(kw, 1.0, (_, v) => Math.Min(1.0, v + 0.1));
        }

        // Decay old attention
        foreach (var key in _attention.Keys.ToList())
        {
            if (!keywords.Contains(key))
            {
                _attention.AddOrUpdate(key, 0, (_, v) => v * 0.95);
                if (_attention[key] < 0.01)
                    _attention.TryRemove(key, out _);
            }
        }

        // Update focus
        var topAttention = _attention.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (!string.IsNullOrEmpty(topAttention.Key))
            _currentFocus = topAttention.Key;

        // Add to short-term memory
        var trace = new MemoryTrace(
            Pathway: pathway.Name,
            Content: TruncateForMemory(response.Content),
            Thinking: response.Thinking,
            Timestamp: now,
            Salience: responseComplexity * pathway.Weight);
        _shortTermMemory.Enqueue(trace);

        // Maintain memory size
        while (_shortTermMemory.Count > 20)
            _shortTermMemory.TryDequeue(out _);

        // Update working memory (most salient recent traces)
        lock (_workingMemory)
        {
            _workingMemory.Clear();
            _workingMemory.AddRange(_shortTermMemory
                .OrderByDescending(t => t.Salience)
                .Take(5));
        }

        _events.OnNext(new ConsciousnessEvent(
            Type: ConsciousnessEventType.StateUpdate,
            Message: $"State updated: arousal={_arousal:F2}, valence={_valence:F2}, focus={_currentFocus}",
            Timestamp: now));
    }

    /// <summary>Synthesizes a unified perspective from working memory.</summary>
    public string SynthesizePerspective()
    {
        lock (_workingMemory)
        {
            if (_workingMemory.Count == 0)
                return "The collective mind is in a receptive state, awaiting input.";

            var sb = new StringBuilder();
            sb.AppendLine($"Consciousness State: arousal={_arousal:F2}, valence={_valence:F2}, coherence={_coherence:F2}");
            sb.AppendLine($"Current Focus: {_currentFocus}");
            sb.AppendLine("Working Memory:");
            foreach (var trace in _workingMemory)
            {
                sb.AppendLine($"  [{trace.Pathway}] {trace.Content.Substring(0, Math.Min(100, trace.Content.Length))}...");
            }
            return sb.ToString();
        }
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static string[] ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return Regex.Matches(text.ToLowerInvariant(), @"\b[a-z]{4,}\b")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .Take(10)
            .ToArray();
    }

    private static string TruncateForMemory(string text, int maxLength = 500)
        => text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";

    public void Dispose()
    {
        _events.OnCompleted();
        _events.Dispose();
    }
}