namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Factory for creating RTSP clients for multiple cameras.
/// </summary>
public interface ITapoRtspClientFactory
{
    /// <summary>
    /// Gets an RTSP client for a specific camera by name.
    /// </summary>
    TapoRtspClient? GetClient(string cameraName);

    /// <summary>
    /// Gets all available camera names.
    /// </summary>
    IEnumerable<string> GetCameraNames();
}