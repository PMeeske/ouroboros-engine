namespace Ouroboros.Network;

/// <summary>
/// Represents the delta between two global network state snapshots.
/// </summary>
/// <param name="FromEpoch">The starting epoch.</param>
/// <param name="ToEpoch">The ending epoch.</param>
/// <param name="NodeDelta">The change in node count.</param>
/// <param name="TransitionDelta">The change in transition count.</param>
/// <param name="Timestamp">The timestamp when the delta was computed.</param>
public sealed record GlobalNetworkStateDelta(
    long FromEpoch,
    long ToEpoch,
    int NodeDelta,
    int TransitionDelta,
    DateTimeOffset Timestamp);