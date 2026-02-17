namespace Ouroboros.Providers;

/// <summary>
/// Naive ensemble that routes requests based on simple heuristics. Real routing
/// logic is outside the scope of the repair, but preserving the public surface
/// lets CLI switches keep working.
/// </summary>
public sealed class MultiModelRouter : Ouroboros.Abstractions.Core.IChatCompletionModel
{
    private readonly IReadOnlyDictionary<string, Ouroboros.Abstractions.Core.IChatCompletionModel> _models;
    private readonly string _fallbackKey;

    public MultiModelRouter(IReadOnlyDictionary<string, Ouroboros.Abstractions.Core.IChatCompletionModel> models, string fallbackKey)
    {
        if (models.Count == 0) throw new ArgumentException("At least one model is required", nameof(models));
        _models = models;
        _fallbackKey = fallbackKey;
    }

    /// <inheritdoc/>
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        Ouroboros.Abstractions.Core.IChatCompletionModel target = SelectModel(prompt);
        return target.GenerateTextAsync(prompt, ct);
    }

    private Ouroboros.Abstractions.Core.IChatCompletionModel SelectModel(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return _models[_fallbackKey];
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase) && _models.TryGetValue("coder", out Ouroboros.Abstractions.Core.IChatCompletionModel? coder))
            return coder;
        if (prompt.Length > 600 && _models.TryGetValue("summarize", out Ouroboros.Abstractions.Core.IChatCompletionModel? summarize))
            return summarize;
        if (prompt.Contains("reason", StringComparison.OrdinalIgnoreCase) && _models.TryGetValue("reason", out Ouroboros.Abstractions.Core.IChatCompletionModel? reason))
            return reason;
        return _models.TryGetValue(_fallbackKey, out Ouroboros.Abstractions.Core.IChatCompletionModel? fallback) ? fallback : _models.Values.First();
    }
}