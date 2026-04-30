// <copyright file="TapoEmbodimentProvider.Inventory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Device inventory management: RefreshRtsp/DeviceInventory, DetermineCapabilities, and device-type predicates.
/// </summary>
public sealed partial class TapoEmbodimentProvider
{
    private Task RefreshRtspCameraInventoryAsync(CancellationToken ct = default)
    {
        _sensors.Clear();
        _actuators.Clear();

        if (_rtspClientFactory == null)
        {
            return Task.CompletedTask;
        }

        foreach (var cameraName in _rtspClientFactory.GetCameraNames())
        {
            var rtspClient = _rtspClientFactory.GetClient(cameraName);
            if (rtspClient == null)
            {
                continue;
            }

            var sensorCapabilities = EmbodimentCapabilities.VideoCapture | EmbodimentCapabilities.VisionAnalysis;
            _sensors[cameraName] = new SensorInfo(
                cameraName,
                $"Tapo Camera ({cameraName})",
                SensorModality.Visual,
                false,
                sensorCapabilities,
                new Dictionary<string, object>
                {
                    ["deviceType"] = "RTSP Camera",
                    ["ipAddress"] = rtspClient.CameraIp,
                    ["rtspUrl"] = rtspClient.RtspUrl,
                    ["visionModel"] = _visionConfig.VisionModel,
                });

            var ptzActuatorId = $"{cameraName}-ptz";
            if (_ptzClients.ContainsKey(cameraName))
            {
                var ptzActions = new List<string>
                {
                    "pan_left", "pan_right", "tilt_up", "tilt_down",
                    "ptz_move", "ptz_stop", "ptz_home",
                    "ptz_go_to_preset", "ptz_set_preset", "ptz_patrol_sweep",
                };

                _actuators[ptzActuatorId] = new ActuatorInfo(
                    ptzActuatorId,
                    $"Tapo Camera PTZ ({cameraName})",
                    ActuatorModality.Motor,
                    true,
                    EmbodimentCapabilities.PTZControl,
                    ptzActions,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = "RTSP Camera PTZ",
                        ["ipAddress"] = rtspClient.CameraIp,
                        ["panRange"] = "360Â°",
                        ["tiltRange"] = "114Â°",
                    });

                _logger?.LogInformation(
                    "Registered PTZ actuator for camera: {CameraName} at {Ip}",
                    cameraName, rtspClient.CameraIp);
            }

            _logger?.LogInformation(
                "Registered RTSP camera: {CameraName} at {Ip}",
                cameraName, rtspClient.CameraIp);
        }

        return Task.CompletedTask;
    }

    private async Task RefreshDeviceInventoryAsync(CancellationToken ct)
    {
        _sensors.Clear();
        _actuators.Clear();

        if (_tapoClient == null)
        {
            return;
        }

        var devicesResult = await _tapoClient.GetDevicesAsync(ct).ConfigureAwait(false);
        if (devicesResult.IsFailure)
        {
            _logger?.LogWarning("Could not refresh device inventory: {Error}", devicesResult.Error);
            return;
        }

        foreach (var device in devicesResult.Value)
        {
            var deviceId = device.Name;

            if (IsCameraDevice(device.DeviceType))
            {
                _sensors[deviceId] = new SensorInfo(
                    deviceId,
                    $"Tapo Camera ({device.Name})",
                    SensorModality.Visual,
                    false,
                    EmbodimentCapabilities.VideoCapture | EmbodimentCapabilities.VisionAnalysis,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress,
                        ["visionModel"] = _visionConfig.VisionModel,
                    });

                _sensors[$"{deviceId}-mic"] = new SensorInfo(
                    $"{deviceId}-mic",
                    $"Tapo Camera Mic ({device.Name})",
                    SensorModality.Audio,
                    false,
                    EmbodimentCapabilities.AudioCapture | EmbodimentCapabilities.TwoWayAudio,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress,
                    });

                _actuators[$"{deviceId}-speaker"] = new ActuatorInfo(
                    $"{deviceId}-speaker",
                    $"Tapo Camera Speaker ({device.Name})",
                    ActuatorModality.Voice,
                    false,
                    EmbodimentCapabilities.AudioOutput | EmbodimentCapabilities.TwoWayAudio,
                    ["speak"],
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                    });
            }
            else if (IsLightDevice(device.DeviceType))
            {
                var supportedActions = new List<string> { "turn_on", "turn_off", "set_brightness" };
                if (IsColorLightDevice(device.DeviceType))
                {
                    supportedActions.Add("set_color");
                    supportedActions.Add("set_color_temperature");
                }

                _actuators[deviceId] = new ActuatorInfo(
                    deviceId,
                    $"Tapo Light ({device.Name})",
                    ActuatorModality.Visual,
                    false,
                    EmbodimentCapabilities.LightingControl,
                    supportedActions,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress,
                    });
            }
            else if (IsPlugDevice(device.DeviceType))
            {
                _actuators[deviceId] = new ActuatorInfo(
                    deviceId,
                    $"Tapo Plug ({device.Name})",
                    ActuatorModality.Motor,
                    false,
                    EmbodimentCapabilities.PowerControl,
                    ["turn_on", "turn_off"],
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress,
                    });
            }
        }

        _logger?.LogInformation(
            "Refreshed device inventory: {SensorCount} sensors, {ActuatorCount} actuators",
            _sensors.Count,
            _actuators.Count);
    }

    private EmbodimentCapabilities DetermineCapabilities()
    {
        var caps = EmbodimentCapabilities.None;

        foreach (var sensor in _sensors.Values)
        {
            caps |= sensor.Capabilities;
        }

        foreach (var actuator in _actuators.Values)
        {
            caps |= actuator.Capabilities;
        }

        if (_visionModel != null)
        {
            caps |= EmbodimentCapabilities.VisionAnalysis;
        }

        if (_ttsModel != null)
        {
            caps |= EmbodimentCapabilities.AudioOutput;
        }

        return caps;
    }

    private static bool IsCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C100 or TapoDeviceType.C200 or TapoDeviceType.C210 or
        TapoDeviceType.C220 or TapoDeviceType.C310 or TapoDeviceType.C320 or
        TapoDeviceType.C420 or TapoDeviceType.C500 or TapoDeviceType.C520;

    private static bool IsLightDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.L510 or TapoDeviceType.L520 or TapoDeviceType.L530 or
        TapoDeviceType.L535 or TapoDeviceType.L610 or TapoDeviceType.L630 or
        TapoDeviceType.L900 or TapoDeviceType.L920 or TapoDeviceType.L930;

    private static bool IsColorLightDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.L530 or TapoDeviceType.L535 or TapoDeviceType.L630 or
        TapoDeviceType.L900 or TapoDeviceType.L920 or TapoDeviceType.L930;

    private static bool IsPlugDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.P100 or TapoDeviceType.P105 or TapoDeviceType.P110 or
        TapoDeviceType.P110M or TapoDeviceType.P115 or TapoDeviceType.P300 or
        TapoDeviceType.P304 or TapoDeviceType.P304M or TapoDeviceType.P316;

    /// <summary>
    /// Determines whether the given device type is a PTZ-capable camera (has pan/tilt motors).
    /// C100 is fixed, C310/C320/C420 are outdoor fixed cameras.
    /// C200, C210, C500, C520 have pan/tilt motors.
    /// </summary>
    private static bool IsPtzCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C200 or TapoDeviceType.C210 or
        TapoDeviceType.C500 or TapoDeviceType.C520;

    /// <summary>
    /// Checks whether a camera is known to be PTZ-capable based on its name or device config.
    /// For RTSP-only cameras (no REST API device list), we check the appsettings device config.
    /// </summary>
    private bool IsCameraNamePtzCapable(string cameraName)
    {
        if (_ptzClients.ContainsKey(cameraName))
        {
            return true;
        }

        var upperName = cameraName.ToUpperInvariant();
        return upperName.Contains("C200") || upperName.Contains("C210") ||
               upperName.Contains("C500") || upperName.Contains("C520");
    }
}
