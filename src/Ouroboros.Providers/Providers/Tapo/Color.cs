using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents RGB color values.
/// </summary>
public sealed record Color
{
    /// <summary>
    /// Gets the red component (0-255).
    /// </summary>
    [JsonPropertyName("red")]
    public required byte Red { get; init; }

    /// <summary>
    /// Gets the green component (0-255).
    /// </summary>
    [JsonPropertyName("green")]
    public required byte Green { get; init; }

    /// <summary>
    /// Gets the blue component (0-255).
    /// </summary>
    [JsonPropertyName("blue")]
    public required byte Blue { get; init; }
}