namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents a captured video frame from a Tapo camera.
/// </summary>
/// <param name="Data">Raw frame data (JPEG).</param>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="FrameNumber">Sequential frame number.</param>
/// <param name="Timestamp">Capture timestamp.</param>
/// <param name="CameraName">Name of the source camera.</param>
public sealed record TapoCameraFrame(
    byte[] Data,
    int Width,
    int Height,
    long FrameNumber,
    DateTime Timestamp,
    string CameraName);