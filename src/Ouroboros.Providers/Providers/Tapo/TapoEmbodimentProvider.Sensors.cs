// <copyright file="TapoEmbodimentProvider.Sensors.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Sensor management: GetState, GetSensors, GetActuators, Activate/Deactivate, ReadSensor, AnalyzeFrame, Authenticate.
/// </summary>
public sealed partial class TapoEmbodimentProvider
{
    /// <inheritdoc/>
    public Task<CanonicalResult<EmbodimentState>> GetStateAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Task.FromResult(CanonicalResult<EmbodimentState>.Failure("Provider is disposed"));
        }

        var state = _isConnected ? EmbodimentState.Awake : EmbodimentState.Dormant;

        if (_activeSensors.Any(s => s.Value))
        {
            state = EmbodimentState.Observing;
        }

        return Task.FromResult(CanonicalResult<EmbodimentState>.Success(state));
    }

    /// <inheritdoc/>
    public Task<CanonicalResult<IReadOnlyList<SensorInfo>>> GetSensorsAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Task.FromResult(CanonicalResult<IReadOnlyList<SensorInfo>>.Failure("Provider is disposed"));
        }

        var sensors = _sensors.Values.ToList();
        return Task.FromResult(CanonicalResult<IReadOnlyList<SensorInfo>>.Success(sensors));
    }

    /// <inheritdoc/>
    public Task<CanonicalResult<IReadOnlyList<ActuatorInfo>>> GetActuatorsAsync(CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Task.FromResult(CanonicalResult<IReadOnlyList<ActuatorInfo>>.Failure("Provider is disposed"));
        }

        var actuators = _actuators.Values.ToList();
        return Task.FromResult(CanonicalResult<IReadOnlyList<ActuatorInfo>>.Success(actuators));
    }

    /// <inheritdoc/>
    public Task<CanonicalResult<Unit>> ActivateSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Task.FromResult(CanonicalResult<Unit>.Failure("Provider is disposed"));
        }

        if (!_isConnected)
        {
            return Task.FromResult(CanonicalResult<Unit>.Failure("Not connected"));
        }

        if (!_sensors.ContainsKey(sensorId))
        {
            return Task.FromResult(CanonicalResult<Unit>.Failure($"Sensor '{sensorId}' not found"));
        }

        _activeSensors[sensorId] = true;

        var sensor = _sensors[sensorId];
        _sensors[sensorId] = sensor with { IsActive = true };

        RaiseEvent(
            EmbodimentProviderEventType.SensorActivated,
            new Dictionary<string, object> { ["sensorId"] = sensorId });

        _logger?.LogInformation("Activated sensor: {SensorId}", sensorId);
        return Task.FromResult(CanonicalResult<Unit>.Success(Unit.Value));
    }

    /// <inheritdoc/>
    public Task<CanonicalResult<Unit>> DeactivateSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Task.FromResult(CanonicalResult<Unit>.Failure("Provider is disposed"));
        }

        if (_activeSensors.ContainsKey(sensorId))
        {
            _activeSensors[sensorId] = false;

            if (_sensors.TryGetValue(sensorId, out var sensor))
            {
                _sensors[sensorId] = sensor with { IsActive = false };
            }

            RaiseEvent(
                EmbodimentProviderEventType.SensorDeactivated,
                new Dictionary<string, object> { ["sensorId"] = sensorId });
        }

        return Task.FromResult(CanonicalResult<Unit>.Success(Unit.Value));
    }

    /// <inheritdoc/>
    public Task<CanonicalResult<PerceptionData>> ReadSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Task.FromResult(CanonicalResult<PerceptionData>.Failure("Provider is disposed"));
        }

        if (!_isConnected)
        {
            return Task.FromResult(CanonicalResult<PerceptionData>.Failure("Not connected"));
        }

        if (!_sensors.TryGetValue(sensorId, out var sensor))
        {
            return Task.FromResult(CanonicalResult<PerceptionData>.Failure($"Sensor '{sensorId}' not found"));
        }

        if (!_activeSensors.GetValueOrDefault(sensorId, false))
        {
            return Task.FromResult(CanonicalResult<PerceptionData>.Failure($"Sensor '{sensorId}' is not active"));
        }

        if (_rtspClientFactory != null && sensor.Modality == SensorModality.Visual)
        {
            return ReadRtspCameraFrameAsync(sensorId, sensor, ct);
        }

        return Task.FromResult(CanonicalResult<PerceptionData>.Failure(
            $"Sensor data retrieval is not implemented for sensor '{sensorId}' in TapoEmbodimentProvider"));
    }

    /// <summary>
    /// Reads a frame from an RTSP camera sensor.
    /// </summary>
    private async Task<CanonicalResult<PerceptionData>> ReadRtspCameraFrameAsync(
        string sensorId,
        SensorInfo sensor,
                CancellationToken ct)
    {
        var cameraName = sensorId;
        var rtspClient = _rtspClientFactory?.GetClient(cameraName);

        if (rtspClient == null)
        {
            return CanonicalResult<PerceptionData>.Failure($"RTSP client not found for camera '{cameraName}'");
        }

        var frameResult = await rtspClient.CaptureFrameAsync(ct).ConfigureAwait(false);
        if (frameResult.IsFailure)
        {
            return CanonicalResult<PerceptionData>.Failure($"Frame capture failed: {frameResult.Error}");
        }

        var frame = frameResult.Value;

        var perception = new PerceptionData(
            SensorId: sensorId,
            Modality: SensorModality.Visual,
            Timestamp: frame.Timestamp,
            Data: frame.Data,
            Metadata: new Dictionary<string, object>
            {
                ["width"] = frame.Width,
                ["height"] = frame.Height,
                ["frameNumber"] = frame.FrameNumber,
                ["cameraName"] = frame.CameraName,
                ["contentType"] = "image/jpeg",
            });

        _perceptions.OnNext(perception);

        _logger?.LogDebug(
            "Captured frame from {Camera}: {Width}x{Height}, {Size} bytes",
            cameraName, frame.Width, frame.Height, frame.Data.Length);

        return CanonicalResult<PerceptionData>.Success(perception);
    }

    /// <summary>
    /// Processes a video frame through the vision model.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<CanonicalResult<VisionAnalysisResult>> AnalyzeFrameAsync(
        string sensorId,
        byte[] frameData,
        CancellationToken ct = default)
    {
        if (_disposed)
        {
            return CanonicalResult<VisionAnalysisResult>.Failure("Provider is disposed");
        }

        if (_visionModel == null)
        {
            return CanonicalResult<VisionAnalysisResult>.Failure("Vision model not available");
        }

        var options = new VisionAnalysisOptions(
            IncludeDescription: true,
            DetectObjects: _visionConfig.EnableObjectDetection,
            DetectFaces: _visionConfig.EnableFaceDetection,
            ClassifyScene: _visionConfig.EnableSceneClassification,
            MaxObjects: _visionConfig.MaxObjectsPerFrame,
            ConfidenceThreshold: _visionConfig.ConfidenceThreshold);

        var result = await _visionModel.AnalyzeImageAsync(frameData, "jpeg", options, ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var perception = new PerceptionData(
                sensorId,
                SensorModality.Visual,
                DateTime.UtcNow,
                result.Value,
                new Dictionary<string, object>
                {
                    ["visionModel"] = _visionConfig.VisionModel,
                    ["description"] = result.Value.Description,
                });

            _perceptions.OnNext(perception);

            return CanonicalResult<VisionAnalysisResult>.Success(result.Value);
        }

        return CanonicalResult<VisionAnalysisResult>.Failure(result.Error);
    }

    /// <summary>
    /// Authenticates with the Tapo REST API server.
    /// </summary>
    /// <param name="serverPassword">The server password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<CanonicalResult<string>> AuthenticateAsync(string serverPassword, CancellationToken ct = default)
    {
        if (_disposed)
        {
            return CanonicalResult<string>.Failure("Provider is disposed");
        }

        if (_tapoClient == null)
        {
            return CanonicalResult<string>.Failure("REST client not configured - using RTSP mode");
        }

        var result = await _tapoClient.LoginAsync(serverPassword, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RefreshDeviceInventoryAsync(ct).ConfigureAwait(false);
        }

        return result;
    }
}
