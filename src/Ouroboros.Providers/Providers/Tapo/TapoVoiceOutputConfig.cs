namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Configuration for Tapo voice output through compatible devices.
/// </summary>
/// <param name="DeviceName">Name of the device with speaker capabilities.</param>
/// <param name="Volume">Volume level (0-100).</param>
/// <param name="SampleRate">Audio sample rate for TTS output.</param>
public sealed record TapoVoiceOutputConfig(
    string DeviceName,
    int Volume = 75,
    int SampleRate = 16000);