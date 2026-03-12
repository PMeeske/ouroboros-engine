// <copyright file="KalmanFilter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Perception;

/// <summary>
/// Standalone one-dimensional Kalman filter for scalar state estimation.
/// Provides predict and update steps following the standard Kalman filter
/// equations. Suitable for tracking a single numeric quantity (e.g.,
/// confidence, latency, sentiment score) that evolves over time with
/// noisy measurements.
/// </summary>
public sealed class KalmanFilter
{
    /// <summary>Gets the current state estimate.</summary>
    public double Estimate { get; private set; }

    /// <summary>Gets the current estimation error covariance.</summary>
    public double ErrorCovariance { get; private set; }

    /// <summary>Gets the Kalman gain from the most recent update step.</summary>
    public double KalmanGain { get; private set; }

    /// <summary>Gets the number of update steps performed.</summary>
    public int UpdateCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KalmanFilter"/> class.
    /// </summary>
    /// <param name="initialEstimate">Initial state estimate.</param>
    /// <param name="initialErrorCovariance">Initial estimation error covariance.</param>
    public KalmanFilter(double initialEstimate = 0.0, double initialErrorCovariance = 1.0)
    {
        Estimate = initialEstimate;
        ErrorCovariance = initialErrorCovariance;
    }

    /// <summary>
    /// Prediction step: advances the state estimate forward in time.
    /// Increases uncertainty by adding process noise to the error covariance.
    /// The state estimate itself is unchanged (assumes no control input / constant model).
    /// </summary>
    /// <param name="processNoise">
    /// Process noise variance (Q). Represents uncertainty in the state transition.
    /// Higher values mean the filter expects more change between steps.
    /// </param>
    public void Predict(double processNoise = 0.01)
    {
        // x_predicted = x (no state transition model — identity)
        // P_predicted = P + Q
        ErrorCovariance += processNoise;
    }

    /// <summary>
    /// Update step: incorporates a new measurement to refine the state estimate.
    /// Computes the Kalman gain, updates the estimate, and reduces error covariance.
    /// </summary>
    /// <param name="measurement">The observed measurement value.</param>
    /// <param name="measurementNoise">
    /// Measurement noise variance (R). Lower values indicate a more precise sensor.
    /// Must be greater than zero.
    /// </param>
    public void Update(double measurement, double measurementNoise = 0.1)
    {
        // Clamp to avoid division by zero
        measurementNoise = Math.Max(measurementNoise, 1e-10);

        // K = P / (P + R)
        KalmanGain = ErrorCovariance / (ErrorCovariance + measurementNoise);

        // x = x + K * (z - x)
        Estimate += KalmanGain * (measurement - Estimate);

        // P = (1 - K) * P
        ErrorCovariance = (1.0 - KalmanGain) * ErrorCovariance;

        UpdateCount++;
    }

    /// <summary>
    /// Performs a combined predict-then-update step in one call.
    /// Convenience method for the common case where predict and update
    /// happen at the same cadence.
    /// </summary>
    /// <param name="measurement">The observed measurement value.</param>
    /// <param name="processNoise">Process noise variance (Q).</param>
    /// <param name="measurementNoise">Measurement noise variance (R).</param>
    public void Step(double measurement, double processNoise = 0.01, double measurementNoise = 0.1)
    {
        Predict(processNoise);
        Update(measurement, measurementNoise);
    }

    /// <summary>
    /// Resets the filter to a new initial state.
    /// </summary>
    /// <param name="estimate">New initial estimate.</param>
    /// <param name="errorCovariance">New initial error covariance.</param>
    public void Reset(double estimate = 0.0, double errorCovariance = 1.0)
    {
        Estimate = estimate;
        ErrorCovariance = errorCovariance;
        KalmanGain = 0;
        UpdateCount = 0;
    }
}
