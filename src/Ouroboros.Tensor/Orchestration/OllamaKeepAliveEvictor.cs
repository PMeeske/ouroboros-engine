// <copyright file="OllamaKeepAliveEvictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Tensor.Orchestration;
/// <summary>
/// Cooperative evictor for Ollama-backed tenants. Unloads the resident model by posting
/// <c>{ "model": ..., "prompt": "", "keep_alive": 0 }</c> to <c>/api/generate</c>,
/// which tells Ollama to drop the model from VRAM immediately.
/// </summary>
public sealed class OllamaKeepAliveEvictor : IEvictionPolicy, IDisposable
{
    private readonly HttpClient _client;
    private readonly string? _model;
    private readonly long _estimatedVramBytes;
    private bool _disposed;

    /// <inheritdoc/>
    public string TenantName { get; }

    /// <inheritdoc/>
    public TimeSpan EstimatedEvictionLatency => TimeSpan.FromMilliseconds(200);

    /// <inheritdoc/>
    public TimeSpan EstimatedReloadLatency => TimeSpan.FromSeconds(3);

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaKeepAliveEvictor"/> class.
    /// Initializes a new <see cref="OllamaKeepAliveEvictor"/>.
    /// </summary>
    /// <param name="tenantName">Tenant name.</param>
    /// <param name="endpoint">Ollama base endpoint (e.g. <c>http://localhost:11434</c>).</param>
    /// <param name="model">Model name to unload (e.g. <c>llama3:latest</c>). Optional — if omitted the eviction returns 0.</param>
    /// <param name="estimatedVramBytes">Estimated bytes reclaimed when the model is unloaded.</param>
    public OllamaKeepAliveEvictor(
        string tenantName,
        string endpoint,
        string? model = null,
        long estimatedVramBytes = 0)
    {
        TenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint is required", nameof(endpoint));
        }

        _model = model;
        _estimatedVramBytes = estimatedVramBytes;
        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    /// <inheritdoc/>
    public bool CanEvictNow() => !_disposed;

    /// <inheritdoc/>
    public async Task<long> EvictAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(_model))
        {
            return 0L;
        }

        var payload = new
        {
            model = _model,
            prompt = string.Empty,
            keep_alive = 0,
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _client
                .PostAsync("/api/generate", content, cancellationToken)
                .ConfigureAwait(false);

            // A success response means Ollama accepted the request and will unload.
            response.EnsureSuccessStatusCode();
            return _estimatedVramBytes;
        }
        catch (HttpRequestException)
        {
            // If Ollama is unreachable we report 0 bytes reclaimed so the
            // coordinator can continue with the next victim.
            return 0L;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _client.Dispose();
        }
    }
}
