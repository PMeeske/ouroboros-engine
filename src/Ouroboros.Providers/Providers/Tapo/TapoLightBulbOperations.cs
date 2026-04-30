using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo light bulb devices (L510, L520, L610).
/// </summary>
public sealed class TapoLightBulbOperations : TapoDeviceOperationsBase
{
    internal TapoLightBulbOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the light bulb on.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync("l510/on", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the light bulb off.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync("l510/off", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the brightness level of the light bulb.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="level">Brightness level (0-100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> SetBrightnessAsync(string deviceName, byte level, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        if (level > 100)
        {
            return Result<Unit>.Failure("Brightness level must be between 0 and 100");
        }

        return await ExecuteActionAsync($"l510/set-brightness?level={level}", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<JsonDocument>.Failure("Device name is required");
        }

        return await GetJsonResponseAsync("l510/get-device-info", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<JsonDocument>.Failure("Device name is required");
        }

        return await GetJsonResponseAsync("l510/get-device-usage", deviceName, ct).ConfigureAwait(false);
    }
}
