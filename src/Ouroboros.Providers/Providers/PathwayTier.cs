namespace Ouroboros.Providers;

/// <summary>
/// Classification of a pathway's capability tier.
/// </summary>
public enum PathwayTier
{
    /// <summary>Local models (Ollama, local inference) - fast, free, good for simple tasks.</summary>
    Local,
    /// <summary>Lightweight cloud models - balanced speed/quality.</summary>
    CloudLight,
    /// <summary>Premium cloud models - highest quality, most expensive.</summary>
    CloudPremium,
    /// <summary>Specialized models (coding, math, etc.).</summary>
    Specialized
}