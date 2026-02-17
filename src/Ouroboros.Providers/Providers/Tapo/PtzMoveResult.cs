namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Result of a PTZ movement operation.
/// </summary>
/// <param name="Success">Whether the movement succeeded.</param>
/// <param name="Direction">Direction of movement.</param>
/// <param name="Duration">How long the movement took.</param>
/// <param name="Message">Descriptive message.</param>
public sealed record PtzMoveResult(
    bool Success,
    string Direction,
    TimeSpan Duration,
    string? Message = null);