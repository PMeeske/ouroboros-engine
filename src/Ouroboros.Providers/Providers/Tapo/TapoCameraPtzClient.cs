// <copyright file="TapoCameraPtzClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// PTZ (Pan/Tilt/Zoom) controller for Tapo cameras with motor capabilities.
/// Uses ONVIF Profile S for standardized PTZ control.
/// Supported cameras: C200 (360° pan, 114° tilt), C210, C500, C520.
/// </summary>
public sealed partial class TapoCameraPtzClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TapoCameraPtzClient>? _logger;
    private readonly string _cameraIp;
    private readonly string _username;
    private readonly string _password;
    private readonly string _onvifUrl;
    private string? _profileToken;
    private bool _disposed;

    /// <summary>
    /// Pan/tilt speed range: -1.0 (full reverse) to 1.0 (full forward).
    /// </summary>
    private const float MaxSpeed = 1.0f;

    /// <summary>
    /// Default movement duration in milliseconds.
    /// </summary>
    private const int DefaultMoveDurationMs = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoCameraPtzClient"/> class.
    /// </summary>
    /// <param name="cameraIp">IP address of the Tapo camera.</param>
    /// <param name="username">Camera account username.</param>
    /// <param name="password">Camera account password.</param>
    /// <param name="logger">Optional logger.</param>
    public TapoCameraPtzClient(
        string cameraIp,
        string username,
        string password,
        ILogger<TapoCameraPtzClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cameraIp);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        _cameraIp = cameraIp;
        _username = username;
        _password = password;
        _logger = logger;
        _onvifUrl = $"http://{_cameraIp}:2020/onvif/ptz_service";

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Gets the camera IP address.
    /// </summary>
    public string CameraIp => _cameraIp;

    /// <summary>
    /// Gets the PTZ capabilities of this camera.
    /// </summary>
    public PtzCapabilities Capabilities { get; private set; } = PtzCapabilities.Default;

    /// <summary>
    /// Initializes the PTZ client by discovering the ONVIF profile token.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<PtzCapabilities>> InitializeAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzCapabilities>.Failure("Client is disposed");

        try
        {
            _logger?.LogInformation("Initializing PTZ for camera at {CameraIp}", _cameraIp);

            // Try ONVIF GetProfiles to discover media profile token
            var profileResult = await GetOnvifProfileTokenAsync(ct).ConfigureAwait(false);
            if (profileResult.IsSuccess)
            {
                _profileToken = profileResult.Value;
                _logger?.LogInformation("ONVIF profile token: {Token}", _profileToken);
            }
            else
            {
                // Default profile token for Tapo cameras
                _profileToken = "profile_1";
                _logger?.LogWarning(
                    "Could not discover ONVIF profile, using default: {Token}. Error: {Error}",
                    _profileToken, profileResult.Error);
            }

            // Determine capabilities based on camera model
            Capabilities = new PtzCapabilities(
                CanPan: true,
                CanTilt: true,
                CanZoom: false, // C200 has no optical zoom
                PanRange: (-1.0f, 1.0f),
                TiltRange: (-1.0f, 1.0f),
                ZoomRange: (0f, 0f),
                SupportsAbsoluteMove: false, // Tapo only supports relative/continuous
                SupportsContinuousMove: true,
                SupportsRelativeMove: true,
                SupportsPresets: true,
                MaxPresets: 8);

            _logger?.LogInformation("PTZ initialized for {CameraIp}: Pan={Pan}, Tilt={Tilt}",
                _cameraIp, Capabilities.CanPan, Capabilities.CanTilt);

            return Result<PtzCapabilities>.Success(Capabilities);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to initialize PTZ for {CameraIp}", _cameraIp);
            return Result<PtzCapabilities>.Failure($"PTZ initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pans the camera left at the specified speed.
    /// </summary>
    /// <param name="speed">Pan speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> PanLeftAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(-Math.Abs(speed), 0, 0, durationMs, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Pans the camera right at the specified speed.
    /// </summary>
    /// <param name="speed">Pan speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> PanRightAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(Math.Abs(speed), 0, 0, durationMs, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tilts the camera up at the specified speed.
    /// </summary>
    /// <param name="speed">Tilt speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> TiltUpAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(0, Math.Abs(speed), 0, durationMs, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tilts the camera down at the specified speed.
    /// </summary>
    /// <param name="speed">Tilt speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> TiltDownAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(0, -Math.Abs(speed), 0, durationMs, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves the camera diagonally (combined pan and tilt).
    /// </summary>
    /// <param name="panSpeed">Pan speed (-1.0 left to 1.0 right).</param>
    /// <param name="tiltSpeed">Tilt speed (-1.0 down to 1.0 up).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> MoveAsync(
        float panSpeed,
        float tiltSpeed,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(panSpeed, tiltSpeed, 0, durationMs, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops all PTZ movement immediately.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> StopAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifStop(_profileToken ?? "profile_1");
            var result = await SendOnvifCommandAsync(soapBody, ct).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger?.LogDebug("PTZ stopped on {CameraIp}", _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "stop", TimeSpan.Zero, "Movement stopped"));
            }

            return Result<PtzMoveResult>.Failure($"Stop failed: {result.Error}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to stop PTZ on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Stop failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves the camera to its home position (factory default center).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> GoToHomeAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifGotoHomePosition(_profileToken ?? "profile_1");
            var result = await SendOnvifCommandAsync(soapBody, ct).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger?.LogInformation("Moving to home position on {CameraIp}", _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "go_home", TimeSpan.Zero, "Moving to home position"));
            }

            return Result<PtzMoveResult>.Failure($"Go home failed: {result.Error}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to go home on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Go home failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current position as a preset.
    /// </summary>
    /// <param name="presetName">Name for the preset.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> SetPresetAsync(
        string presetName,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifSetPreset(_profileToken ?? "profile_1", presetName);
            var result = await SendOnvifCommandAsync(soapBody, ct).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger?.LogInformation("Preset '{Preset}' saved on {CameraIp}", presetName, _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "set_preset", TimeSpan.Zero, $"Preset '{presetName}' saved"));
            }

            return Result<PtzMoveResult>.Failure($"Set preset failed: {result.Error}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to set preset on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Set preset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves the camera to a saved preset position.
    /// </summary>
    /// <param name="presetToken">Preset token or number.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> GoToPresetAsync(
        string presetToken,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifGotoPreset(_profileToken ?? "profile_1", presetToken);
            var result = await SendOnvifCommandAsync(soapBody, ct).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger?.LogInformation("Moving to preset '{Preset}' on {CameraIp}", presetToken, _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "go_to_preset", TimeSpan.Zero, $"Moving to preset '{presetToken}'"));
            }

            return Result<PtzMoveResult>.Failure($"Go to preset failed: {result.Error}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to go to preset on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Go to preset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs a patrol sweep: pan fully left, then fully right, then return home.
    /// Useful for periodic area scanning.
    /// </summary>
    /// <param name="speed">Sweep speed (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> PatrolSweepAsync(
        float speed = 0.3f,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            _logger?.LogInformation("Starting patrol sweep on {CameraIp}", _cameraIp);
            var sw = Stopwatch.StartNew();

            // Pan left for 3 seconds
            var leftResult = await ContinuousMoveAsync(-speed, 0, 0, 3000, ct).ConfigureAwait(false);
            if (leftResult.IsFailure) return leftResult;

            // Pan right for 6 seconds (left + center + right)
            var rightResult = await ContinuousMoveAsync(speed, 0, 0, 6000, ct).ConfigureAwait(false);
            if (rightResult.IsFailure) return rightResult;

            // Return to center
            var centerResult = await ContinuousMoveAsync(-speed, 0, 0, 3000, ct).ConfigureAwait(false);
            if (centerResult.IsFailure) return centerResult;

            sw.Stop();
            _logger?.LogInformation("Patrol sweep completed in {Duration}ms", sw.ElapsedMilliseconds);

            return Result<PtzMoveResult>.Success(
                new PtzMoveResult(true, "patrol_sweep", sw.Elapsed, "Patrol sweep completed"));
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Patrol sweep failed on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Patrol sweep failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests PTZ connectivity by sending a stop command.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var stopResult = await StopAsync(ct).ConfigureAwait(false);
            return stopResult.IsSuccess
                ? Result<string>.Success($"PTZ control available at {_cameraIp}")
                : Result<string>.Failure($"PTZ not responding: {stopResult.Error}");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string>.Failure($"PTZ connection test failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
        _logger?.LogDebug("TapoCameraPtzClient disposed for {CameraIp}", _cameraIp);
    }
}
