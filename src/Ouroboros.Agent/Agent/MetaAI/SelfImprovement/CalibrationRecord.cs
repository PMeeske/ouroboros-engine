namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a recorded prediction for calibration tracking.
/// </summary>
internal sealed record CalibrationRecord(
    double PredictedConfidence,
    bool ActualSuccess,
    DateTime RecordedAt);