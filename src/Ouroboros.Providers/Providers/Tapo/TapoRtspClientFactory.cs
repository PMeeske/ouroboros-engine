using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Default implementation of ITapoRtspClientFactory.
/// </summary>
public sealed class TapoRtspClientFactory : ITapoRtspClientFactory, IDisposable
{
    private readonly Dictionary<string, TapoRtspClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoRtspClientFactory"/> class.
    /// </summary>
    public TapoRtspClientFactory(
        IEnumerable<TapoDevice> cameras,
        string username,
        string password,
        ILogger<TapoRtspClient>? logger = null)
    {
        foreach (var camera in cameras)
        {
            _clients[camera.Name] = new TapoRtspClient(
                camera.IpAddress, username, password, CameraStreamQuality.HD, logger);
        }
    }

    /// <inheritdoc/>
    public TapoRtspClient? GetClient(string cameraName) =>
        _clients.TryGetValue(cameraName, out var client) ? client : null;

    /// <inheritdoc/>
    public IEnumerable<string> GetCameraNames() => _clients.Keys;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
    }
}