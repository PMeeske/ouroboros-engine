// Copyright (c) Ouroboros. All rights reserved.

using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Subset of the device_info response common to plugs, bulbs, and strips.
/// Field names match the device's snake_case wire format. Unknown fields are ignored.
/// </summary>
public sealed record TapoDeviceInfo
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("hw_ver")]
    public string? HardwareVersion { get; init; }

    [JsonPropertyName("fw_ver")]
    public string? FirmwareVersion { get; init; }

    [JsonPropertyName("mac")]
    public string? Mac { get; init; }

    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    [JsonPropertyName("ssid")]
    public string? Ssid { get; init; }

    [JsonPropertyName("rssi")]
    public int? Rssi { get; init; }

    /// <summary>Gets device nickname is base64-encoded UTF-8 on the wire. Use <see cref="Nickname"/>.</summary>
    [JsonPropertyName("nickname")]
    public string? NicknameBase64 { get; init; }

    /// <summary>Gets decoded nickname, or null if the device hasn't set one.</summary>
    [JsonIgnore]
    public string? Nickname => string.IsNullOrEmpty(NicknameBase64)
        ? null
        : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(NicknameBase64));

    [JsonPropertyName("device_on")]
    public bool? DeviceOn { get; init; }

    [JsonPropertyName("on_time")]
    public long? OnTimeSeconds { get; init; }

    [JsonPropertyName("overheated")]
    public bool? Overheated { get; init; }

    [JsonPropertyName("brightness")]
    public int? Brightness { get; init; }

    [JsonPropertyName("color_temp")]
    public int? ColorTemp { get; init; }

    [JsonPropertyName("hue")]
    public int? Hue { get; init; }

    [JsonPropertyName("saturation")]
    public int? Saturation { get; init; }

    [JsonPropertyName("region")]
    public string? Region { get; init; }
}

/// <summary>
/// Generic KLAP response envelope: <c>{ "error_code": 0, "result": {...} }</c>.
/// Error codes are device-defined; 0 is the only success value.
/// </summary>
internal sealed record TapoKlapEnvelope<T>
{
    [JsonPropertyName("error_code")]
    public int ErrorCode { get; init; }

    [JsonPropertyName("result")]
    public T? Result { get; init; }

    [JsonPropertyName("msg")]
    public string? Message { get; init; }
}

/// <summary>
/// Generic KLAP request envelope.
/// </summary>
internal sealed record TapoKlapRequest
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public object? Params { get; init; }

    [JsonPropertyName("requestTimeMils")]
    public long RequestTimeMillis { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("terminalUUID")]
    public string? TerminalUuid { get; init; }
}
