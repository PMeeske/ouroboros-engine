using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Camera stream quality settings.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CameraStreamQuality
{
    /// <summary>Low quality stream for reduced bandwidth.</summary>
    Low,

    /// <summary>Standard definition stream.</summary>
    Standard,

    /// <summary>High definition 720p stream.</summary>
    HD,

    /// <summary>Full HD 1080p stream.</summary>
    FullHD,

    /// <summary>2K QHD stream.</summary>
    QHD
}