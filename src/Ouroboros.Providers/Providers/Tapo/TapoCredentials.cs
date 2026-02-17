using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents Tapo account credentials (server-side configuration only).
/// </summary>
public sealed record TapoCredentials
{
    /// <summary>
    /// Gets the Tapo account email address.
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>
    /// Gets the Tapo account password.
    /// </summary>
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}