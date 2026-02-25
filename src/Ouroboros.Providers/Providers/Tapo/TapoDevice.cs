using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents a Tapo device configuration.
/// </summary>
public sealed record TapoDevice
{
    /// <summary>
    /// Gets the name of the device.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the device type.
    /// </summary>
    [JsonPropertyName("device_type")]
    public required TapoDeviceType DeviceType { get; init; }

    /// <summary>
    /// Gets the IP address of the device.
    /// </summary>
    [JsonPropertyName("ip_addr")]
    public required string IpAddress { get; init; }
}