namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Configuration for Tapo camera embodiment.
/// </summary>
/// <param name="CameraName">Name of the camera device.</param>
/// <param name="StreamQuality">Video stream quality.</param>
/// <param name="EnableAudio">Whether to enable audio capture.</param>
/// <param name="EnableMotionDetection">Whether to enable motion detection.</param>
/// <param name="EnablePersonDetection">Whether to enable AI person detection.</param>
/// <param name="FrameRate">Target frame rate for video capture.</param>
/// <param name="VisionModel">Vision model to use for analysis (e.g., llava:13b).</param>
public sealed record TapoCameraConfig(
    string CameraName,
    CameraStreamQuality StreamQuality = CameraStreamQuality.HD,
    bool EnableAudio = true,
    bool EnableMotionDetection = true,
    bool EnablePersonDetection = true,
    int FrameRate = 15,
    string VisionModel = "llava:13b");