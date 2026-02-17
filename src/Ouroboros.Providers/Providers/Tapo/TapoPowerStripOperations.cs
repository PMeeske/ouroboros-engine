using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo power strip devices (P300, P304, P304M, P316).
/// </summary>
public sealed class TapoPowerStripOperations : TapoDeviceOperationsBase
{
    internal TapoPowerStripOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p300/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets the list of child devices (plugs) on the power strip.
    /// </summary>
    public async Task<Result<JsonDocument>> GetChildDeviceListAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p300/get-child-device-list", deviceName, ct);
    }

}