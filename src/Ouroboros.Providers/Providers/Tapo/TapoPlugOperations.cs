using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo smart plug devices (P100, P105).
/// </summary>
public sealed class TapoPlugOperations : TapoDeviceOperationsBase
{
    internal TapoPlugOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the plug on.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync("p100/on", deviceName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the plug off.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return Result<Unit>.Failure("Device name is required");
        }

        return await ExecuteActionAsync("p100/off", deviceName, ct).ConfigureAwait(false);
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

        return await GetJsonResponseAsync("p100/get-device-info", deviceName, ct).ConfigureAwait(false);
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

        return await GetJsonResponseAsync("p100/get-device-usage", deviceName, ct).ConfigureAwait(false);
    }
}
