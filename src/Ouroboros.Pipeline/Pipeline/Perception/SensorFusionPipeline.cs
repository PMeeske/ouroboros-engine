// <copyright file="SensorFusionPipeline.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Perception;

/// <summary>
/// Multimodal sensor fusion pipeline using confidence-weighted Kalman-inspired estimation.
/// Combines multiple sensor modalities (text, vision, audio, numeric telemetry, etc.)
/// into unified state estimates. Each sensor reading is tracked per sensor-modality
/// pair with independent Kalman state, and anomaly detection flags readings that
/// deviate significantly from the current estimate.
/// </summary>
public sealed class SensorFusionPipeline
{
    /// <summary>A single sensor reading from one modality.</summary>
    /// <param name="SensorId">Identifier of the sensor source.</param>
    /// <param name="Modality">Modality type (e.g., "text", "vision", "audio").</param>
    /// <param name="Value">Numeric value of the reading.</param>
    /// <param name="Confidence">Confidence in the reading [0, 1].</param>
    /// <param name="Timestamp">When the reading was captured.</param>
    /// <param name="Metadata">Optional additional metadata.</param>
    public sealed record SensorReading(
        string SensorId,
        string Modality,
        double Value,
        double Confidence,
        DateTime Timestamp,
        Dictionary<string, object>? Metadata = null);

    /// <summary>The fused state estimate across all tracked sensors.</summary>
    /// <param name="EstimatedValues">Current estimated value per sensor-modality key.</param>
    /// <param name="Confidences">Current confidence per sensor-modality key.</param>
    /// <param name="Timestamp">When this fused state was computed.</param>
    /// <param name="SourceCount">Number of tracked sensor-modality pairs.</param>
    public sealed record FusedState(
        Dictionary<string, double> EstimatedValues,
        Dictionary<string, double> Confidences,
        DateTime Timestamp,
        int SourceCount);

    private readonly Dictionary<string, KalmanState> _states = [];
    private readonly object _lock = new();

    /// <summary>Default process noise injected at each Kalman prediction step.</summary>
    private const double DefaultProcessNoise = 0.01;

    private sealed record KalmanState(
        double Estimate,
        double ErrorCovariance,
        DateTime LastUpdate);

    /// <summary>
    /// Ingests a sensor reading and updates the internal Kalman state for that
    /// sensor-modality pair. The first reading initializes state; subsequent
    /// readings perform a Kalman update step.
    /// </summary>
    /// <param name="reading">The sensor reading to ingest.</param>
    public void IngestReading(SensorReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        lock (_lock)
        {
            var key = FormatKey(reading.SensorId, reading.Modality);

            if (!_states.TryGetValue(key, out var state))
            {
                // Initialize with first reading
                _states[key] = new KalmanState(
                    reading.Value,
                    1.0 - reading.Confidence,
                    reading.Timestamp);
                return;
            }

            // Kalman update
            var predictedError = state.ErrorCovariance + DefaultProcessNoise;
            var measurementNoise = Math.Max(1.0 - reading.Confidence, 0.001);
            var kalmanGain = predictedError / (predictedError + measurementNoise);
            var newEstimate = state.Estimate + (kalmanGain * (reading.Value - state.Estimate));
            var newError = (1.0 - kalmanGain) * predictedError;

            _states[key] = new KalmanState(newEstimate, newError, reading.Timestamp);
        }
    }

    /// <summary>
    /// Ingests multiple readings in a single batch operation.
    /// </summary>
    /// <param name="readings">The readings to ingest.</param>
    public void IngestBatch(IEnumerable<SensorReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        foreach (var reading in readings)
        {
            IngestReading(reading);
        }
    }

    /// <summary>
    /// Gets the current fused state across all tracked sensors.
    /// Confidence is derived from error covariance: lower error = higher confidence.
    /// </summary>
    /// <returns>The fused state estimate.</returns>
    public FusedState GetFusedState()
    {
        lock (_lock)
        {
            var values = new Dictionary<string, double>(_states.Count);
            var confidences = new Dictionary<string, double>(_states.Count);

            foreach (var (key, state) in _states)
            {
                values[key] = state.Estimate;
                confidences[key] = Math.Clamp(1.0 - state.ErrorCovariance, 0, 1);
            }

            return new FusedState(values, confidences, DateTime.UtcNow, _states.Count);
        }
    }

    /// <summary>
    /// Fuses multiple readings of the same concept from different modalities
    /// using confidence-weighted averaging. Higher-confidence readings
    /// contribute more to the fused value.
    /// </summary>
    /// <param name="readings">Readings from multiple modalities to fuse.</param>
    /// <returns>The confidence-weighted fused value.</returns>
    public static double FuseMultiModal(IEnumerable<SensorReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var list = readings.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var totalWeight = list.Sum(r => r.Confidence);
        if (totalWeight <= 0)
        {
            return list.Average(r => r.Value);
        }

        return list.Sum(r => r.Value * r.Confidence) / totalWeight;
    }

    /// <summary>
    /// Detects anomalous readings by comparing each reading against its
    /// tracked Kalman estimate. A reading is anomalous when its deviation
    /// from the estimate exceeds <paramref name="threshold"/> standard deviations.
    /// </summary>
    /// <param name="readings">Readings to check for anomalies.</param>
    /// <param name="threshold">Number of standard deviations to flag (default 2.0).</param>
    /// <returns>List of readings identified as anomalous.</returns>
    public List<SensorReading> DetectAnomalies(IEnumerable<SensorReading> readings, double threshold = 2.0)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var anomalies = new List<SensorReading>();

        lock (_lock)
        {
            foreach (var reading in readings)
            {
                var key = FormatKey(reading.SensorId, reading.Modality);

                if (_states.TryGetValue(key, out var state))
                {
                    var deviation = Math.Abs(reading.Value - state.Estimate);
                    var stdDev = Math.Sqrt(state.ErrorCovariance);

                    if (stdDev > 0 && deviation / stdDev > threshold)
                    {
                        anomalies.Add(reading);
                    }
                }
            }
        }

        return anomalies;
    }

    /// <summary>
    /// Gets the current estimate for a specific sensor-modality pair, if tracked.
    /// </summary>
    /// <param name="sensorId">The sensor identifier.</param>
    /// <param name="modality">The modality.</param>
    /// <returns>The current estimate, or null if not tracked.</returns>
    public double? GetEstimate(string sensorId, string modality)
    {
        lock (_lock)
        {
            return _states.TryGetValue(FormatKey(sensorId, modality), out var state)
                ? state.Estimate
                : null;
        }
    }

    /// <summary>Resets all tracked sensor states.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _states.Clear();
        }
    }

    private static string FormatKey(string sensorId, string modality)
        => $"{sensorId}:{modality}";
}
