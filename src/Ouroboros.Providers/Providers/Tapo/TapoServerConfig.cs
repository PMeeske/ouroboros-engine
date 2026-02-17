using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents the server-side configuration for the Tapo REST API server.
/// This is NOT used by the client but documents what the server requires.
/// The server configuration file needs tapo_credentials (email/password),
/// server_password, and device list.
/// </summary>
/// <remarks>
/// Example server configuration (server-side only, not for client use):
/// <code>
/// {
///   "tapo_credentials": {
///     "email": "your-tapo-account@example.com",
///     "password": "your-tapo-account-password"
///   },
///   "server_password": "your-api-server-password",
///   "devices": [
///     {
///       "name": "living-room-bulb",
///       "device_type": "L530",
///       "ip_addr": "192.168.1.100"
///     }
///   ]
/// }
/// </code>
/// </remarks>
public sealed record TapoServerConfig
{
    /// <summary>
    /// Gets the Tapo account credentials (server-side only).
    /// </summary>
    [JsonPropertyName("tapo_credentials")]
    public required TapoCredentials TapoCredentials { get; init; }

    /// <summary>
    /// Gets the server password used for client authentication.
    /// </summary>
    [JsonPropertyName("server_password")]
    public required string ServerPassword { get; init; }

    /// <summary>
    /// Gets the list of configured devices.
    /// </summary>
    [JsonPropertyName("devices")]
    public required List<TapoDevice> Devices { get; init; }
}