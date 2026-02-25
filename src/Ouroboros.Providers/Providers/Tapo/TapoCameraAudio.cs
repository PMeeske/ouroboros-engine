namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents captured audio data from a Tapo camera.
/// </summary>
/// <param name="Data">Raw audio data (PCM).</param>
/// <param name="SampleRate">Audio sample rate in Hz.</param>
/// <param name="Channels">Number of audio channels.</param>
/// <param name="Duration">Duration of the audio segment.</param>
/// <param name="Timestamp">Capture timestamp.</param>
/// <param name="CameraName">Name of the source camera.</param>
public sealed record TapoCameraAudio(
    byte[] Data,
    int SampleRate,
    int Channels,
    TimeSpan Duration,
    DateTime Timestamp,
    string CameraName);