// <copyright file="TapoEmbodimentProvider.Actions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Actuator action execution: ExecuteActionAsync, PTZ control, TurnOn/Off, SetColor, Speech.
/// </summary>
public sealed partial class TapoEmbodimentProvider
{
    /// <inheritdoc/>
    public async Task<Result<ActionOutcome>> ExecuteActionAsync(
        string actuatorId,
        ActuatorAction action,
        CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Result<ActionOutcome>.Failure("Provider is disposed");
        }

        if (!_isConnected)
        {
            return Result<ActionOutcome>.Failure("Not connected");
        }

        if (!_actuators.ContainsKey(actuatorId))
        {
            return Result<ActionOutcome>.Failure($"Actuator '{actuatorId}' not found");
        }

        var startTime = DateTime.UtcNow;

        try
        {
            Result<Unit>? result = null;

            switch (action.ActionType.ToLowerInvariant())
            {
                case "turn_on":
                    result = await ExecuteTurnOnAsync(actuatorId, ct).ConfigureAwait(false);
                    break;

                case "turn_off":
                    result = await ExecuteTurnOffAsync(actuatorId, ct).ConfigureAwait(false);
                    break;

                case "set_color":
                    if (action.Parameters != null &&
                        action.Parameters.TryGetValue("red", out var r) &&
                        action.Parameters.TryGetValue("green", out var g) &&
                        action.Parameters.TryGetValue("blue", out var b))
                    {
                        result = await ExecuteSetColorAsync(
                            actuatorId,
                            Convert.ToByte(r),
                            Convert.ToByte(g),
                            Convert.ToByte(b),
                            ct).ConfigureAwait(false);
                    }
                    else
                    {
                        return Result<ActionOutcome>.Failure("Color parameters required");
                    }

                    break;

                case "speak":
                    if (_ttsModel != null && action.Parameters?.TryGetValue("text", out var text) == true)
                    {
                        var emotion = action.Parameters.TryGetValue("emotion", out var e) ? e?.ToString() : null;
                        var speechResult = await SynthesizeSpeechAsync(text?.ToString() ?? string.Empty, emotion, ct).ConfigureAwait(false);

                        var duration = DateTime.UtcNow - startTime;
                        return Result<ActionOutcome>.Success(new ActionOutcome(
                            actuatorId,
                            action.ActionType,
                            speechResult.IsSuccess,
                            duration,
                            speechResult.IsSuccess ? speechResult.Value : null,
                            speechResult.IsFailure ? speechResult.Error : null));
                    }
                    else
                    {
                        return Result<ActionOutcome>.Failure("TTS model not available or text not provided");
                    }

                case "pan_left":
                case "pan_right":
                case "tilt_up":
                case "tilt_down":
                case "ptz_move":
                case "ptz_stop":
                case "ptz_home":
                case "ptz_go_to_preset":
                case "ptz_set_preset":
                case "ptz_patrol_sweep":
                {
                    var ptzResult = await ExecutePtzActionAsync(actuatorId, action, ct).ConfigureAwait(false);
                    var ptzElapsed = DateTime.UtcNow - startTime;
                    if (ptzResult.IsSuccess)
                    {
                        return Result<ActionOutcome>.Success(new ActionOutcome(
                            actuatorId,
                            action.ActionType,
                            ptzResult.Value.Success,
                            ptzElapsed,
                            ptzResult.Value,
                            ptzResult.Value.Success ? null : ptzResult.Value.Message));
                    }

                    return Result<ActionOutcome>.Success(new ActionOutcome(
                        actuatorId,
                        action.ActionType,
                        false,
                        ptzElapsed,
                        Error: ptzResult.Error));
                }

                default:
                    return Result<ActionOutcome>.Failure($"Unsupported action type: {action.ActionType}");
            }

            var elapsed = DateTime.UtcNow - startTime;

            if (result?.IsSuccess == true)
            {
                _logger?.LogInformation(
                    "Executed action {Action} on actuator {Actuator} in {Duration}ms",
                    action.ActionType,
                    actuatorId,
                    elapsed.TotalMilliseconds);

                return Result<ActionOutcome>.Success(new ActionOutcome(
                    actuatorId,
                    action.ActionType,
                    true,
                    elapsed));
            }

            return Result<ActionOutcome>.Success(new ActionOutcome(
                actuatorId,
                action.ActionType,
                false,
                elapsed,
                Error: result?.Error ?? "Unknown error"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger?.LogError(ex, "Failed to execute action {Action} on actuator {Actuator}",
                action.ActionType, actuatorId);

            return Result<ActionOutcome>.Success(new ActionOutcome(
                actuatorId,
                action.ActionType,
                false,
                elapsed,
                Error: ex.Message));
        }
    }

    private async Task<Result<Unit>> ExecuteTurnOnAsync(string deviceId, CancellationToken ct)
    {
        Result<Unit> colorResult = await _tapoClient!.ColorLightBulbs.TurnOnAsync(deviceId, ct).ConfigureAwait(false);
        if (colorResult.IsSuccess)
        {
            return colorResult;
        }

        Result<Unit> bulbResult = await _tapoClient!.LightBulbs.TurnOnAsync(deviceId, ct).ConfigureAwait(false);
        if (bulbResult.IsSuccess)
        {
            return bulbResult;
        }

        return await _tapoClient!.Plugs.TurnOnAsync(deviceId, ct).ConfigureAwait(false);
    }

    private async Task<Result<Unit>> ExecuteTurnOffAsync(string deviceId, CancellationToken ct)
    {
        Result<Unit> colorResult = await _tapoClient!.ColorLightBulbs.TurnOffAsync(deviceId, ct).ConfigureAwait(false);
        if (colorResult.IsSuccess)
        {
            return colorResult;
        }

        Result<Unit> bulbResult = await _tapoClient!.LightBulbs.TurnOffAsync(deviceId, ct).ConfigureAwait(false);
        if (bulbResult.IsSuccess)
        {
            return bulbResult;
        }

        return await _tapoClient!.Plugs.TurnOffAsync(deviceId, ct).ConfigureAwait(false);
    }

    private Task<Result<Unit>> ExecuteSetColorAsync(
        string deviceId, byte r, byte g, byte b, CancellationToken ct)
    {
        return _tapoClient!.ColorLightBulbs.SetColorAsync(
            deviceId,
            new Color { Red = r, Green = g, Blue = b },
            ct);
    }

    private async Task<Result<SynthesizedSpeech>> SynthesizeSpeechAsync(
        string text, string? emotion, CancellationToken ct)
    {
        if (_ttsModel == null)
        {
            return Result<SynthesizedSpeech>.Failure("TTS model not available");
        }

        var config = new VoiceConfig(Style: emotion ?? "neutral");
        var result = await _ttsModel.SynthesizeAsync(text, config, ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return Result<SynthesizedSpeech>.Success(result.Value);
        }

        return Result<SynthesizedSpeech>.Failure(result.Error);
    }

    private async Task<Result<PtzMoveResult>> ExecutePtzActionAsync(
        string actuatorId,
        ActuatorAction action,
        CancellationToken ct)
    {
        var cameraName = actuatorId.EndsWith("-ptz", StringComparison.OrdinalIgnoreCase)
            ? actuatorId[..^4]
            : actuatorId;

        if (!_ptzClients.TryGetValue(cameraName, out var ptzClient))
        {
            return Result<PtzMoveResult>.Failure(
                $"No PTZ client available for camera '{cameraName}'");
        }

        var speed = GetFloatParam(action, "speed", 0.5f);
        var durationMs = GetIntParam(action, "duration_ms", 500);

        return action.ActionType.ToLowerInvariant() switch
        {
            "pan_left" => await ptzClient.PanLeftAsync(speed, durationMs, ct).ConfigureAwait(false),
            "pan_right" => await ptzClient.PanRightAsync(speed, durationMs, ct).ConfigureAwait(false),
            "tilt_up" => await ptzClient.TiltUpAsync(speed, durationMs, ct).ConfigureAwait(false),
            "tilt_down" => await ptzClient.TiltDownAsync(speed, durationMs, ct).ConfigureAwait(false),
            "ptz_move" => await ptzClient.MoveAsync(
                GetFloatParam(action, "pan_speed", 0f),
                GetFloatParam(action, "tilt_speed", 0f),
                durationMs, ct).ConfigureAwait(false),
            "ptz_stop" => await ptzClient.StopAsync(ct).ConfigureAwait(false),
            "ptz_home" => await ptzClient.GoToHomeAsync(ct).ConfigureAwait(false),
            "ptz_go_to_preset" => await ptzClient.GoToPresetAsync(
                GetStringParam(action, "preset_token", "1"), ct).ConfigureAwait(false),
            "ptz_set_preset" => await ptzClient.SetPresetAsync(
                GetStringParam(action, "preset_name", "preset_1"), ct).ConfigureAwait(false),
            "ptz_patrol_sweep" => await ptzClient.PatrolSweepAsync(speed, ct).ConfigureAwait(false),
            _ => Result<PtzMoveResult>.Failure($"Unknown PTZ action: {action.ActionType}"),
        };
    }

    private async Task InitializePtzClientsAsync(CancellationToken ct)
    {
        if (_rtspClientFactory == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
        {
            _logger?.LogWarning("PTZ credentials not provided, skipping PTZ initialization");
            return;
        }

        foreach (var cameraName in _rtspClientFactory.GetCameraNames())
        {
            var rtspClient = _rtspClientFactory.GetClient(cameraName);
            if (rtspClient == null)
            {
                continue;
            }

            await InitializeSinglePtzClientAsync(cameraName, rtspClient.CameraIp, ct).ConfigureAwait(false);
        }
    }

    private async Task InitializeSinglePtzClientAsync(
        string cameraName, string cameraIp, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
        {
            return;
        }

        TapoCameraPtzClient? ptzClient = new TapoCameraPtzClient(
            cameraIp,
            _username,
            _password);
        try
        {
            var initResult = await ptzClient.InitializeAsync(ct).ConfigureAwait(false);
            if (initResult.IsSuccess)
            {
                _ptzClients[cameraName] = ptzClient;
                ptzClient = null; // Ownership transferred
                _logger?.LogInformation(
                    "PTZ control initialized for camera {CameraName} at {Ip}",
                    cameraName, cameraIp);
            }
            else
            {
                _logger?.LogWarning(
                    "PTZ not available for camera {CameraName}: {Error}",
                    cameraName, initResult.Error);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(
                ex,
                "Failed to initialize PTZ for camera {CameraName}", cameraName);
        }
        finally
        {
            ptzClient?.Dispose();
        }
    }

    private static float GetFloatParam(ActuatorAction action, string key, float defaultValue)
    {
        if (action.Parameters?.TryGetValue(key, out var value) == true)
        {
            return Convert.ToSingle(value);
        }

        return defaultValue;
    }

    private static int GetIntParam(ActuatorAction action, string key, int defaultValue)
    {
        if (action.Parameters?.TryGetValue(key, out var value) == true)
        {
            return Convert.ToInt32(value);
        }

        return defaultValue;
    }

    private static string GetStringParam(ActuatorAction action, string key, string defaultValue)
    {
        if (action.Parameters?.TryGetValue(key, out var value) == true)
        {
            return value?.ToString() ?? defaultValue;
        }

        return defaultValue;
    }
}
