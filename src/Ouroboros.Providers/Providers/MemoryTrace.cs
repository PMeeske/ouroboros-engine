namespace Ouroboros.Providers;

/// <summary>A trace in the consciousness memory stream.</summary>
public sealed record MemoryTrace(
    string Pathway,
    string Content,
    string? Thinking,
    DateTime Timestamp,
    double Salience);