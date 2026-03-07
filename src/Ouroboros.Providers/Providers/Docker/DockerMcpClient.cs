// <copyright file="DockerMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Ouroboros.Providers.Json;

namespace Ouroboros.Providers.Docker;

/// <summary>
/// Docker Engine MCP client using the Docker Engine REST API.
/// Supports Unix socket, named pipe, and TCP connections.
/// </summary>
public sealed partial class DockerMcpClient : IDockerMcpClient, IDisposable
{
    private readonly DockerMcpClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerMcpClient"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="httpClient">Optional HTTP client (for TCP mode or testing).</param>
    public DockerMcpClient(DockerMcpClientOptions options, HttpClient? httpClient = null)
    {
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid DockerMcpClientOptions", nameof(options));
        }

        _options = options;
        _httpClient = httpClient ?? CreateHttpClient(options);
        _jsonOptions = JsonDefaults.CamelCase;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DockerContainerInfo>, string>> ListContainersAsync(
        bool all = false,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/{_options.ApiVersion}/containers/json?all={all.ToString().ToLowerInvariant()}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<DockerContainerInfo>, string>.Failure(
                    $"Failed to list containers: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonDocument.Parse(json).RootElement;
            var containers = new List<DockerContainerInfo>();

            foreach (var item in items.EnumerateArray())
                containers.Add(ParseContainerSummary(item));

            return Result<IReadOnlyList<DockerContainerInfo>, string>.Success(containers);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<DockerContainerInfo>, string>.Failure($"ListContainers failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<DockerContainerInfo>, string>.Failure($"ListContainers failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<DockerContainerInfo, string>> InspectContainerAsync(
        string containerId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_options.ApiVersion}/containers/{containerId}/json", ct);
            if (!response.IsSuccessStatusCode)
                return Result<DockerContainerInfo, string>.Failure(
                    $"Failed to inspect container '{containerId}': {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var el = JsonDocument.Parse(json).RootElement;
            return Result<DockerContainerInfo, string>.Success(ParseContainerInspect(el));
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<DockerContainerInfo, string>.Failure($"InspectContainer failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<DockerContainerInfo, string>.Failure($"InspectContainer failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> GetContainerLogsAsync(
        string containerId,
        int tail = 100,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/{_options.ApiVersion}/containers/{containerId}/logs?stdout=true&stderr=true&tail={tail}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to get logs for '{containerId}': {response.StatusCode}");

            var logs = await response.Content.ReadAsStringAsync(ct);
            return Result<string, string>.Success(StripDockerStreamHeaders(logs));
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"GetContainerLogs failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> StartContainerAsync(
        string containerId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/{_options.ApiVersion}/containers/{containerId}/start", null, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                return Result<string, string>.Success($"Container '{containerId}' already running");

            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to start container '{containerId}': {response.StatusCode}");

            return Result<string, string>.Success($"Container '{containerId}' started");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"StartContainer failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> StopContainerAsync(
        string containerId,
        int timeoutSeconds = 10,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/{_options.ApiVersion}/containers/{containerId}/stop?t={timeoutSeconds}", null, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                return Result<string, string>.Success($"Container '{containerId}' already stopped");

            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to stop container '{containerId}': {response.StatusCode}");

            return Result<string, string>.Success($"Container '{containerId}' stopped");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"StopContainer failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> RemoveContainerAsync(
        string containerId,
        bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/{_options.ApiVersion}/containers/{containerId}?force={force.ToString().ToLowerInvariant()}";
            var response = await _httpClient.DeleteAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to remove container '{containerId}': {response.StatusCode}");

            return Result<string, string>.Success($"Container '{containerId}' removed");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"RemoveContainer failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DockerImageInfo>, string>> ListImagesAsync(
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_options.ApiVersion}/images/json", ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<DockerImageInfo>, string>.Failure(
                    $"Failed to list images: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonDocument.Parse(json).RootElement;
            var images = new List<DockerImageInfo>();

            foreach (var item in items.EnumerateArray())
            {
                var tags = new List<string>();
                if (item.TryGetProperty("RepoTags", out var rt) && rt.ValueKind == JsonValueKind.Array)
                    foreach (var t in rt.EnumerateArray())
                        if (t.GetString() is { } s && s != "<none>:<none>")
                            tags.Add(s);

                images.Add(new DockerImageInfo
                {
                    Id = item.GetProperty("Id").GetString()!,
                    RepoTags = tags,
                    Size = item.TryGetProperty("Size", out var sz) ? sz.GetInt64() : 0,
                    CreatedAt = item.TryGetProperty("Created", out var cr)
                        ? DateTimeOffset.FromUnixTimeSeconds(cr.GetInt64()) : null
                });
            }

            return Result<IReadOnlyList<DockerImageInfo>, string>.Success(images);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<DockerImageInfo>, string>.Failure($"ListImages failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<DockerImageInfo>, string>.Failure($"ListImages failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> PullImageAsync(
        string image,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/{_options.ApiVersion}/images/create?fromImage={Uri.EscapeDataString(image)}", null, ct);

            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to pull image '{image}': {response.StatusCode}");

            // Docker sends streaming JSON; read all to confirm success
            await response.Content.ReadAsStringAsync(ct);
            return Result<string, string>.Success($"Image '{image}' pulled successfully");
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"PullImage failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DockerNetworkInfo>, string>> ListNetworksAsync(
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_options.ApiVersion}/networks", ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<DockerNetworkInfo>, string>.Failure(
                    $"Failed to list networks: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonDocument.Parse(json).RootElement;
            var networks = new List<DockerNetworkInfo>();

            foreach (var item in items.EnumerateArray())
            {
                networks.Add(new DockerNetworkInfo
                {
                    Id = item.GetProperty("Id").GetString()!,
                    Name = item.GetProperty("Name").GetString()!,
                    Driver = item.TryGetProperty("Driver", out var d) ? d.GetString()! : "unknown",
                    Scope = item.TryGetProperty("Scope", out var sc) ? sc.GetString() : null
                });
            }

            return Result<IReadOnlyList<DockerNetworkInfo>, string>.Success(networks);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<DockerNetworkInfo>, string>.Failure($"ListNetworks failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<DockerNetworkInfo>, string>.Failure($"ListNetworks failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DockerVolumeInfo>, string>> ListVolumesAsync(
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/{_options.ApiVersion}/volumes", ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<DockerVolumeInfo>, string>.Failure(
                    $"Failed to list volumes: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json).RootElement;
            var volumes = new List<DockerVolumeInfo>();

            if (doc.TryGetProperty("Volumes", out var vols) && vols.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vols.EnumerateArray())
                {
                    var labels = new Dictionary<string, string>();
                    if (v.TryGetProperty("Labels", out var lbl) && lbl.ValueKind == JsonValueKind.Object)
                        foreach (var kv in lbl.EnumerateObject())
                            labels[kv.Name] = kv.Value.GetString()!;

                    volumes.Add(new DockerVolumeInfo
                    {
                        Name = v.GetProperty("Name").GetString()!,
                        Driver = v.TryGetProperty("Driver", out var dr) ? dr.GetString()! : "local",
                        Mountpoint = v.TryGetProperty("Mountpoint", out var mp) ? mp.GetString() : null,
                        Labels = labels
                    });
                }
            }

            return Result<IReadOnlyList<DockerVolumeInfo>, string>.Success(volumes);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<DockerVolumeInfo>, string>.Failure($"ListVolumes failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<DockerVolumeInfo>, string>.Failure($"ListVolumes failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> RunContainerAsync(
        string image,
        string? name = null,
        IReadOnlyList<string>? ports = null,
        IReadOnlyList<string>? envVars = null,
        CancellationToken ct = default)
    {
        try
        {
            // Build port bindings
            var exposedPorts = new Dictionary<string, object>();
            var portBindings = new Dictionary<string, object[]>();

            if (ports != null)
            {
                foreach (var mapping in ports)
                {
                    var parts = mapping.Split(':');
                    if (parts.Length == 2)
                    {
                        var containerPort = $"{parts[1]}/tcp";
                        exposedPorts[containerPort] = new { };
                        portBindings[containerPort] = new object[]
                        {
                            new { HostIp = "0.0.0.0", HostPort = parts[0] }
                        };
                    }
                }
            }

            var createBody = new
            {
                Image = image,
                Env = envVars ?? (IReadOnlyList<string>)Array.Empty<string>(),
                ExposedPorts = exposedPorts,
                HostConfig = new { PortBindings = portBindings }
            };

            var nameQuery = !string.IsNullOrWhiteSpace(name) ? $"?name={Uri.EscapeDataString(name)}" : "";
            var content = new StringContent(
                JsonSerializer.Serialize(createBody, _jsonOptions),
                Encoding.UTF8, "application/json");

            // Create
            var createResponse = await _httpClient.PostAsync(
                $"/{_options.ApiVersion}/containers/create{nameQuery}", content, ct);

            if (!createResponse.IsSuccessStatusCode)
            {
                var err = await createResponse.Content.ReadAsStringAsync(ct);
                return Result<string, string>.Failure($"Failed to create container: {createResponse.StatusCode} — {err}");
            }

            var createJson = await createResponse.Content.ReadAsStringAsync(ct);
            var createDoc = JsonDocument.Parse(createJson);
            var containerId = createDoc.RootElement.GetProperty("Id").GetString()!;

            // Start
            var startResponse = await _httpClient.PostAsync(
                $"/{_options.ApiVersion}/containers/{containerId}/start", null, ct);

            if (!startResponse.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Container created ({containerId[..12]}) but failed to start: {startResponse.StatusCode}");

            return Result<string, string>.Success(containerId);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"RunContainer failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"RunContainer failed: {ex.Message}");
        }
    }

    /// <summary>Disposes managed resources.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }

}
