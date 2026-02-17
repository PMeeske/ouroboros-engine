namespace Ouroboros.Providers;

/// <summary>A consciousness event.</summary>
public sealed record ConsciousnessEvent(
    ConsciousnessEventType Type,
    string Message,
    DateTime Timestamp);