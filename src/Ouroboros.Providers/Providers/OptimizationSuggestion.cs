namespace Ouroboros.Providers;

/// <summary>
/// An optimization suggestion from the election system.
/// </summary>
public sealed record OptimizationSuggestion(
    string ModelName,
    OptimizationType Type,
    string Reason,
    int Priority);