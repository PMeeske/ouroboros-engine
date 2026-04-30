using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo RGB light strip devices (L900).
/// </summary>
public sealed class TapoLightStripOperations : TapoDeviceOperationsBase
{
    internal TapoLightStripOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the light strip on.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync("l900/on", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the light strip off.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync("l900/off", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the brightness level.
    /// </summary>
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

        return await ExecuteActionAsync($"l900/set-brightness?level={level}", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the color using RGB values.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> SetColorAsync(string deviceName, Color color, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync(
            $"l900/set-color?color.red={color.Red}&color.green={color.Green}&color.blue={color.Blue}",
            deviceName,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the hue and saturation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> SetHueSaturationAsync(string deviceName, ushort hue, byte saturation, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        if (hue > 360)
        {
            return Result<Unit>.Failure("Hue must be between 0 and 360");
        }

        if (saturation > 100)
        {
            return Result<Unit>.Failure("Saturation must be between 0 and 100");
        }

        return await ExecuteActionAsync($"l900/set-hue-saturation?hue={hue}&saturation={saturation}", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the color temperature.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> SetColorTemperatureAsync(string deviceName, ushort temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync($"l900/set-color-temperature?color_temperature={temperature}", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<JsonDocument>.Failure("Device name is required");
        }

        return await GetJsonResponseAsync("l900/get-device-info", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<JsonDocument>.Failure("Device name is required");
        }

        return await GetJsonResponseAsync("l900/get-device-usage", deviceName, ct).ConfigureAwait(false);
    }
}
