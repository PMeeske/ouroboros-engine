namespace Ouroboros.Providers;

/// <summary>
/// A monad representing a response candidate with its evaluation metadata.
/// Supports functional composition via Select, SelectMany, and Where.
/// </summary>
public sealed class ResponseCandidate<T>
{
    public T Value { get; }
    public string Source { get; }
    public double Score { get; private set; }
    public TimeSpan Latency { get; }
    public IReadOnlyDictionary<string, double> Metrics { get; }
    public bool IsValid { get; }

    private ResponseCandidate(T value, string source, double score, TimeSpan latency,
        IReadOnlyDictionary<string, double> metrics, bool isValid)
    {
        Value = value;
        Source = source;
        Score = score;
        Latency = latency;
        Metrics = metrics;
        IsValid = isValid;
    }

    public static ResponseCandidate<T> Create(T value, string source, TimeSpan latency) =>
        new(value, source, 0.0, latency, new Dictionary<string, double>(), true);

    public static ResponseCandidate<T> Invalid(string source) =>
        new(default!, source, 0.0, TimeSpan.Zero, new Dictionary<string, double>(), false);

    public ResponseCandidate<T> WithScore(double score) =>
        new(Value, Source, score, Latency, Metrics, IsValid);

    public ResponseCandidate<T> WithMetrics(IReadOnlyDictionary<string, double> metrics) =>
        new(Value, Source, Score, Latency, metrics, IsValid);

    // Functor: map over the value
    public ResponseCandidate<TResult> Select<TResult>(Func<T, TResult> selector) =>
        IsValid
            ? new ResponseCandidate<TResult>(selector(Value), Source, Score, Latency, Metrics, true)
            : ResponseCandidate<TResult>.Invalid(Source);

    // Monad: flatMap for composition
    public ResponseCandidate<TResult> SelectMany<TResult>(Func<T, ResponseCandidate<TResult>> selector) =>
        IsValid ? selector(Value) : ResponseCandidate<TResult>.Invalid(Source);

    // LINQ support
    public ResponseCandidate<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, ResponseCandidate<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        if (!IsValid) return ResponseCandidate<TResult>.Invalid(Source);
        var intermediate = selector(Value);
        if (!intermediate.IsValid) return ResponseCandidate<TResult>.Invalid(Source);
        return ResponseCandidate<TResult>.Create(
            resultSelector(Value, intermediate.Value), Source, Latency + intermediate.Latency);
    }

    // Filter
    public ResponseCandidate<T> Where(Func<T, bool> predicate) =>
        IsValid && predicate(Value) ? this : Invalid(Source);
}