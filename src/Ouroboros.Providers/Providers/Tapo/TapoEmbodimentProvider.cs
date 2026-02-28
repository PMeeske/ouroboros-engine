// <copyright file="TapoEmbodimentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Embodiment provider that uses Tapo smart devices as the state source.
/// Implements the repository-like IEmbodimentProvider interface, allowing
/// the Tapo REST API to serve as the persistence/state layer for embodiment.
/// Supports direct RTSP camera connections for C200/C210/etc. cameras.
/// </summary>
public sealed partial class TapoEmbodimentProvider : IEmbodimentProvider
{
    private TapoRestClient? _tapoClient;
    private readonly ITapoRtspClientFactory? _rtspClientFactory;
    private readonly IVisionModel? _visionModel;
    private readonly ITtsModel? _ttsModel;
    private readonly ILogger<TapoEmbodimentProvider>? _logger;
    private readonly TapoVisionModelConfig _visionConfig;
    private readonly string? _username;
    private readonly string? _password;

    private readonly Subject<PerceptionData> _perceptions = new();
    private readonly Subject<EmbodimentProviderEvent> _events = new();
    private readonly Dictionary<string, SensorInfo> _sensors = new();
    private readonly Dictionary<string, ActuatorInfo> _actuators = new();
    private readonly Dictionary<string, bool> _activeSensors = new();
    private readonly Dictionary<string, TapoCameraPtzClient> _ptzClients = new();

    private bool _isConnected;
    private bool _disposed;
    private string? _sessionId;
    private EmbodimentCapabilities _capabilities;

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider with REST client.
    /// </summary>
    /// <param name="tapoClient">The Tapo REST API client (state source).</param>
    /// <param name="providerId">Unique provider identifier.</param>
    /// <param name="visionModel">Optional vision model for frame analysis.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="visionConfig">Vision model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    public TapoEmbodimentProvider(
        TapoRestClient tapoClient,
        string providerId = "tapo",
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        TapoVisionModelConfig? visionConfig = null,
        ILogger<TapoEmbodimentProvider>? logger = null)
    {
        _tapoClient = tapoClient ?? throw new ArgumentNullException(nameof(tapoClient));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _visionConfig = visionConfig ?? TapoVisionModelConfig.CreateDefault();
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider with RTSP camera support.
    /// </summary>
    /// <param name="rtspClientFactory">Factory for creating RTSP clients for cameras.</param>
    /// <param name="providerId">Unique provider identifier.</param>
    /// <param name="visionModel">Optional vision model for frame analysis.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="visionConfig">Vision model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="username">Camera account username for PTZ control.</param>
    /// <param name="password">Camera account password for PTZ control.</param>
    public TapoEmbodimentProvider(
        ITapoRtspClientFactory rtspClientFactory,
        string providerId = "tapo-rtsp",
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        TapoVisionModelConfig? visionConfig = null,
        ILogger<TapoEmbodimentProvider>? logger = null,
        string? username = null,
        string? password = null)
    {
        _rtspClientFactory = rtspClientFactory ?? throw new ArgumentNullException(nameof(rtspClientFactory));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _visionConfig = visionConfig ?? TapoVisionModelConfig.CreateDefault();
        _logger = logger;
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider with both REST API and RTSP camera support.
    /// This allows simultaneous access to cameras (via RTSP) and other devices (via REST API).
    /// </summary>
    /// <param name="tapoClient">Optional Tapo REST API client for lights, plugs, etc.</param>
    /// <param name="rtspClientFactory">Optional factory for RTSP camera clients.</param>
    /// <param name="providerId">Unique provider identifier.</param>
    /// <param name="visionModel">Optional vision model for frame analysis.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="visionConfig">Vision model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="username">Camera account username for PTZ control.</param>
    /// <param name="password">Camera account password for PTZ control.</param>
    public TapoEmbodimentProvider(
        TapoRestClient? tapoClient,
        ITapoRtspClientFactory? rtspClientFactory,
        string providerId = "tapo",
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        TapoVisionModelConfig? visionConfig = null,
        ILogger<TapoEmbodimentProvider>? logger = null,
        string? username = null,
        string? password = null)
    {
        if (tapoClient == null && rtspClientFactory == null)
        {
            throw new ArgumentException("At least one of tapoClient or rtspClientFactory must be provided");
        }

        _tapoClient = tapoClient;
        _rtspClientFactory = rtspClientFactory;
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _visionConfig = visionConfig ?? TapoVisionModelConfig.CreateDefault();
        _logger = logger;
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Gets the RTSP client factory (if using RTSP mode).
    /// </summary>
    public ITapoRtspClientFactory? RtspClientFactory => _rtspClientFactory;

    /// <summary>
    /// Gets the REST client (if configured).
    /// </summary>
    public TapoRestClient? RestClient => _tapoClient;

    /// <summary>
    /// Sets the REST client dynamically (e.g., after gateway startup).
    /// </summary>
    public void SetRestClient(TapoRestClient client)
    {
        _tapoClient = client;
    }

    /// <inheritdoc/>
    public string ProviderId { get; }

    /// <inheritdoc/>
    public string ProviderName => "Tapo Smart Devices";

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public IObservable<PerceptionData> Perceptions => _perceptions.AsObservable();

    /// <inheritdoc/>
    public IObservable<EmbodimentProviderEvent> Events => _events.AsObservable();

    /// <inheritdoc/>
    public async Task<Result<EmbodimentCapabilities>> ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<EmbodimentCapabilities>.Failure("Provider is disposed");
        if (_isConnected) return Result<EmbodimentCapabilities>.Success(_capabilities);

        try
        {
            if (_rtspClientFactory != null)
            {
                _logger?.LogInformation("Initializing RTSP camera connections...");
                await InitializePtzClientsAsync(ct);
                await RefreshRtspCameraInventoryAsync(ct);
            }

            if (_tapoClient != null)
            {
                _logger?.LogInformation("Initializing REST API device connections...");
                var devicesResult = await _tapoClient.GetDevicesAsync(ct);

                if (devicesResult.IsFailure)
                {
                    _logger?.LogWarning("Could not get REST API devices: {Error}. Smart home device control may be unavailable.",
                        devicesResult.Error);
                }
                else
                {
                    await RefreshDeviceInventoryAsync(ct);
                }
            }

            if (_rtspClientFactory == null && _tapoClient == null)
            {
                return Result<EmbodimentCapabilities>.Failure("No Tapo client configured");
            }

            _isConnected = true;
            _capabilities = DetermineCapabilities();

            RaiseEvent(EmbodimentProviderEventType.Connected);

            _logger?.LogInformation(
                "Connected to Tapo provider with capabilities: {Capabilities}",
                _capabilities);

            return Result<EmbodimentCapabilities>.Success(_capabilities);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to Tapo provider");
            return Result<EmbodimentCapabilities>.Failure($"Connection failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit>> DisconnectAsync(CancellationToken ct = default)
    {
        if (!_isConnected) return Result<Unit>.Success(Unit.Value);

        try
        {
            _isConnected = false;
            _sensors.Clear();
            _actuators.Clear();
            _activeSensors.Clear();
            _sessionId = null;

            RaiseEvent(EmbodimentProviderEventType.Disconnected);

            await Task.CompletedTask;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disconnecting from Tapo provider");
            return Result<Unit>.Failure($"Disconnect failed: {ex.Message}");
        }
    }

    private void RaiseEvent(
        EmbodimentProviderEventType eventType,
        IReadOnlyDictionary<string, object>? details = null)
    {
        _events.OnNext(new EmbodimentProviderEvent(eventType, DateTime.UtcNow, details));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isConnected = false;
        _sensors.Clear();
        _actuators.Clear();
        _activeSensors.Clear();

        foreach (var ptzClient in _ptzClients.Values)
        {
            ptzClient.Dispose();
        }

        _ptzClients.Clear();

        _perceptions.OnCompleted();
        _events.OnCompleted();

        _perceptions.Dispose();
        _events.Dispose();

        _logger?.LogInformation("TapoEmbodimentProvider disposed");
    }
}
