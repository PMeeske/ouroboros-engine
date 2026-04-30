// <copyright file="KubernetesMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.Json;

namespace Ouroboros.Providers.Kubernetes;

/// <summary>
/// Kubernetes MCP client using the Kubernetes REST API.
/// Supports in-cluster, kubeconfig, and explicit token authentication.
/// </summary>
public sealed partial class KubernetesMcpClient : IKubernetesMcpClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KubernetesMcpClient"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="httpClient">Optional HTTP client.</param>
    public KubernetesMcpClient(KubernetesMcpClientOptions options, HttpClient? httpClient = null)
    {
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid KubernetesMcpClientOptions. Provide BaseUrl+Token, KubeConfigPath, or run in-cluster.", nameof(options));
        }

        _httpClient = httpClient ?? CreateHttpClient(options);
        _jsonOptions = JsonDefaults.CamelCase;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<KubernetesPodInfo>, string>> ListPodsAsync(
        string ns = "default",
        string? labelSelector = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/namespaces/{ns}/pods";
            if (!string.IsNullOrWhiteSpace(labelSelector))
            {
                url += $"?labelSelector={Uri.EscapeDataString(labelSelector)}";
            }

            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<KubernetesPodInfo>, string>.Failure(
                    $"Failed to list pods: {response.StatusCode} — {await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var pods = ParsePodList(doc.RootElement);
            return Result<IReadOnlyList<KubernetesPodInfo>, string>.Success(pods);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<KubernetesPodInfo>, string>.Failure($"ListPods failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<KubernetesPodInfo>, string>.Failure($"ListPods failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<KubernetesPodInfo, string>> GetPodAsync(
        string name,
        string ns = "default",
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/namespaces/{ns}/pods/{name}", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<KubernetesPodInfo, string>.Failure(
                    $"Failed to get pod '{name}': {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            return Result<KubernetesPodInfo, string>.Success(ParsePod(doc.RootElement));
        }
        catch (HttpRequestException ex)
        {
            return Result<KubernetesPodInfo, string>.Failure($"GetPod failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<KubernetesPodInfo, string>.Failure($"GetPod failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> GetPodLogsAsync(
        string podName,
        string ns = "default",
        string? container = null,
        int tailLines = 100,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/namespaces/{ns}/pods/{podName}/log?tailLines={tailLines}";
            if (!string.IsNullOrWhiteSpace(container))
            {
                url += $"&container={Uri.EscapeDataString(container)}";
            }

            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure(
                    $"Failed to get logs for pod '{podName}': {response.StatusCode}");
            }

            var logs = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Result<string, string>.Success(logs);
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"GetPodLogs failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<KubernetesDeploymentInfo>, string>> ListDeploymentsAsync(
        string ns = "default",
        string? labelSelector = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/apis/apps/v1/namespaces/{ns}/deployments";
            if (!string.IsNullOrWhiteSpace(labelSelector))
            {
                url += $"?labelSelector={Uri.EscapeDataString(labelSelector)}";
            }

            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<KubernetesDeploymentInfo>, string>.Failure(
                    $"Failed to list deployments: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var deployments = ParseDeploymentList(doc.RootElement);
            return Result<IReadOnlyList<KubernetesDeploymentInfo>, string>.Success(deployments);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<KubernetesDeploymentInfo>, string>.Failure($"ListDeployments failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<KubernetesDeploymentInfo>, string>.Failure($"ListDeployments failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<KubernetesDeploymentInfo, string>> ScaleDeploymentAsync(
        string deploymentName,
        int replicas,
        string ns = "default",
        CancellationToken ct = default)
    {
        try
        {
            var patch = JsonSerializer.Serialize(
                new
            {
                spec = new { replicas },
            }, _jsonOptions);

            using var content = new StringContent(patch, Encoding.UTF8, "application/strategic-merge-patch+json");
            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"/apis/apps/v1/namespaces/{ns}/deployments/{deploymentName}")
            {
                Content = content,
            };

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<KubernetesDeploymentInfo, string>.Failure(
                    $"Failed to scale deployment '{deploymentName}': {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            return Result<KubernetesDeploymentInfo, string>.Success(ParseDeployment(doc.RootElement));
        }
        catch (HttpRequestException ex)
        {
            return Result<KubernetesDeploymentInfo, string>.Failure($"ScaleDeployment failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<KubernetesDeploymentInfo, string>.Failure($"ScaleDeployment failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<KubernetesServiceInfo>, string>> ListServicesAsync(
        string ns = "default",
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/namespaces/{ns}/services", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<KubernetesServiceInfo>, string>.Failure(
                    $"Failed to list services: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var services = ParseServiceList(doc.RootElement);
            return Result<IReadOnlyList<KubernetesServiceInfo>, string>.Success(services);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<KubernetesServiceInfo>, string>.Failure($"ListServices failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<KubernetesServiceInfo>, string>.Failure($"ListServices failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<string>, string>> ListNamespacesAsync(
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/namespaces", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<string>, string>.Failure(
                    $"Failed to list namespaces: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");
            var names = new List<string>();
            foreach (var item in items.EnumerateArray())
            {
                names.Add(item.GetProperty("metadata").GetProperty("name").GetString()!);
            }

            return Result<IReadOnlyList<string>, string>.Success(names);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<string>, string>.Failure($"ListNamespaces failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<string>, string>.Failure($"ListNamespaces failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> ApplyManifestAsync(
        string manifest,
        string ns = "default",
        CancellationToken ct = default)
    {
        try
        {
            // Parse the manifest to determine kind and apiVersion
            var doc = JsonDocument.Parse(manifest);
            var kind = doc.RootElement.GetProperty("kind").GetString()!.ToLowerInvariant();
            var name = doc.RootElement.GetProperty("metadata").GetProperty("name").GetString()!;
            var apiVersion = doc.RootElement.GetProperty("apiVersion").GetString()!;

            var url = BuildResourceUrl(apiVersion, kind, ns, name);
            using var content = new StringContent(manifest, Encoding.UTF8, "application/json");

            // Try PUT (update), fall back to POST (create) on 404
            var response = await _httpClient.PutAsync(url, content, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var createUrl = BuildResourceUrl(apiVersion, kind, ns);
                response = await _httpClient.PostAsync(createUrl, content, ct).ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure(
                    $"Failed to apply manifest: {response.StatusCode}");
            }

            return Result<string, string>.Success($"{kind}/{name} applied successfully");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"ApplyManifest failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"ApplyManifest failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> DeleteResourceAsync(
        string kind,
        string name,
        string ns = "default",
        CancellationToken ct = default)
    {
        try
        {
            var apiVersion = kind.ToLowerInvariant() switch
            {
                "deployment" or "statefulset" or "daemonset" or "replicaset" => "apps/v1",
                "ingress" => "networking.k8s.io/v1",
                "cronjob" or "job" => "batch/v1",
                _ => "v1",
            };

            var url = BuildResourceUrl(apiVersion, kind.ToLowerInvariant(), ns, name);
            var response = await _httpClient.DeleteAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure(
                    $"Failed to delete {kind}/{name}: {response.StatusCode}");
            }

            return Result<string, string>.Success($"{kind}/{name} deleted");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"DeleteResource failed: {ex.Message}");
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
