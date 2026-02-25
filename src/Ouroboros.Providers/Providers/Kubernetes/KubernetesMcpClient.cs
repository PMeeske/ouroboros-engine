// <copyright file="KubernetesMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Providers.Kubernetes;

/// <summary>
/// Kubernetes MCP client using the Kubernetes REST API.
/// Supports in-cluster, kubeconfig, and explicit token authentication.
/// </summary>
public sealed class KubernetesMcpClient : IKubernetesMcpClient, IDisposable
{
    private readonly KubernetesMcpClientOptions _options;
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

        _options = options;
        _httpClient = httpClient ?? CreateHttpClient(options);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
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
                url += $"?labelSelector={Uri.EscapeDataString(labelSelector)}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<KubernetesPodInfo>, string>.Failure(
                    $"Failed to list pods: {response.StatusCode} — {await response.Content.ReadAsStringAsync(ct)}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var pods = ParsePodList(doc.RootElement);
            return Result<IReadOnlyList<KubernetesPodInfo>, string>.Success(pods);
        }
        catch (Exception ex)
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
            var response = await _httpClient.GetAsync($"/api/v1/namespaces/{ns}/pods/{name}", ct);
            if (!response.IsSuccessStatusCode)
                return Result<KubernetesPodInfo, string>.Failure(
                    $"Failed to get pod '{name}': {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            return Result<KubernetesPodInfo, string>.Success(ParsePod(doc.RootElement));
        }
        catch (Exception ex)
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
                url += $"&container={Uri.EscapeDataString(container)}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to get logs for pod '{podName}': {response.StatusCode}");

            var logs = await response.Content.ReadAsStringAsync(ct);
            return Result<string, string>.Success(logs);
        }
        catch (Exception ex)
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
                url += $"?labelSelector={Uri.EscapeDataString(labelSelector)}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<KubernetesDeploymentInfo>, string>.Failure(
                    $"Failed to list deployments: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var deployments = ParseDeploymentList(doc.RootElement);
            return Result<IReadOnlyList<KubernetesDeploymentInfo>, string>.Success(deployments);
        }
        catch (Exception ex)
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
            var patch = JsonSerializer.Serialize(new
            {
                spec = new { replicas }
            }, _jsonOptions);

            var content = new StringContent(patch, Encoding.UTF8, "application/strategic-merge-patch+json");
            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"/apis/apps/v1/namespaces/{ns}/deployments/{deploymentName}")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return Result<KubernetesDeploymentInfo, string>.Failure(
                    $"Failed to scale deployment '{deploymentName}': {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            return Result<KubernetesDeploymentInfo, string>.Success(ParseDeployment(doc.RootElement));
        }
        catch (Exception ex)
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
            var response = await _httpClient.GetAsync($"/api/v1/namespaces/{ns}/services", ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<KubernetesServiceInfo>, string>.Failure(
                    $"Failed to list services: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var services = ParseServiceList(doc.RootElement);
            return Result<IReadOnlyList<KubernetesServiceInfo>, string>.Success(services);
        }
        catch (Exception ex)
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
            var response = await _httpClient.GetAsync("/api/v1/namespaces", ct);
            if (!response.IsSuccessStatusCode)
                return Result<IReadOnlyList<string>, string>.Failure(
                    $"Failed to list namespaces: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");
            var names = new List<string>();
            foreach (var item in items.EnumerateArray())
            {
                names.Add(item.GetProperty("metadata").GetProperty("name").GetString()!);
            }

            return Result<IReadOnlyList<string>, string>.Success(names);
        }
        catch (Exception ex)
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
            var content = new StringContent(manifest, Encoding.UTF8, "application/json");

            // Try PUT (update), fall back to POST (create) on 404
            var response = await _httpClient.PutAsync(url, content, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var createUrl = BuildResourceUrl(apiVersion, kind, ns);
                response = await _httpClient.PostAsync(createUrl, content, ct);
            }

            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to apply manifest: {response.StatusCode}");

            return Result<string, string>.Success($"{kind}/{name} applied successfully");
        }
        catch (Exception ex)
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
                _ => "v1"
            };

            var url = BuildResourceUrl(apiVersion, kind.ToLowerInvariant(), ns, name);
            var response = await _httpClient.DeleteAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure(
                    $"Failed to delete {kind}/{name}: {response.StatusCode}");

            return Result<string, string>.Success($"{kind}/{name} deleted");
        }
        catch (Exception ex)
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

    // ── Helpers ──────────────────────────────────────────────────────────

    private static HttpClient CreateHttpClient(KubernetesMcpClientOptions options)
    {
        var handler = new HttpClientHandler();
        if (options.SkipTlsVerify)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var client = new HttpClient(handler) { Timeout = options.Timeout };

        // Resolve base URL
        var baseUrl = options.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var host = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            var port = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT") ?? "443";
            baseUrl = $"https://{host}:{port}";
        }

        client.BaseAddress = new Uri(baseUrl);

        // Resolve token
        var token = options.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            const string saTokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
            if (File.Exists(saTokenPath))
                token = File.ReadAllText(saTokenPath).Trim();
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private static string BuildResourceUrl(string apiVersion, string kind, string ns, string? name = null)
    {
        var prefix = apiVersion == "v1" ? "/api/v1" : $"/apis/{apiVersion}";
        var plural = PluralizeKind(kind);
        var url = $"{prefix}/namespaces/{ns}/{plural}";
        if (!string.IsNullOrWhiteSpace(name))
            url += $"/{name}";
        return url;
    }

    private static string PluralizeKind(string kind) => kind.ToLowerInvariant() switch
    {
        "pod" => "pods",
        "service" => "services",
        "deployment" => "deployments",
        "statefulset" => "statefulsets",
        "daemonset" => "daemonsets",
        "replicaset" => "replicasets",
        "configmap" => "configmaps",
        "secret" => "secrets",
        "ingress" => "ingresses",
        "namespace" => "namespaces",
        "job" => "jobs",
        "cronjob" => "cronjobs",
        "persistentvolumeclaim" => "persistentvolumeclaims",
        var s when s.EndsWith('s') => s,
        var s => s + "s"
    };

    private static IReadOnlyList<KubernetesPodInfo> ParsePodList(JsonElement root)
    {
        var pods = new List<KubernetesPodInfo>();
        if (root.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
                pods.Add(ParsePod(item));
        }

        return pods;
    }

    private static KubernetesPodInfo ParsePod(JsonElement el)
    {
        var metadata = el.GetProperty("metadata");
        var status = el.TryGetProperty("status", out var s) ? s : default;
        var spec = el.TryGetProperty("spec", out var sp) ? sp : default;

        var containers = new List<string>();
        if (spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("containers", out var cs))
        {
            foreach (var c in cs.EnumerateArray())
                if (c.TryGetProperty("name", out var cn))
                    containers.Add(cn.GetString()!);
        }

        var restartCount = 0;
        if (status.ValueKind == JsonValueKind.Object && status.TryGetProperty("containerStatuses", out var css))
        {
            foreach (var cs2 in css.EnumerateArray())
                if (cs2.TryGetProperty("restartCount", out var rc))
                    restartCount += rc.GetInt32();
        }

        return new KubernetesPodInfo
        {
            Name = metadata.GetProperty("name").GetString()!,
            Namespace = metadata.GetProperty("namespace").GetString()!,
            Phase = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("phase", out var ph)
                ? ph.GetString()! : "Unknown",
            PodIp = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("podIP", out var ip)
                ? ip.GetString() : null,
            NodeName = spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("nodeName", out var nn)
                ? nn.GetString() : null,
            Labels = ParseLabels(metadata),
            Containers = containers,
            CreatedAt = metadata.TryGetProperty("creationTimestamp", out var ct)
                ? DateTimeOffset.Parse(ct.GetString()!) : null,
            RestartCount = restartCount
        };
    }

    private static IReadOnlyList<KubernetesDeploymentInfo> ParseDeploymentList(JsonElement root)
    {
        var list = new List<KubernetesDeploymentInfo>();
        if (root.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
                list.Add(ParseDeployment(item));
        }

        return list;
    }

    private static KubernetesDeploymentInfo ParseDeployment(JsonElement el)
    {
        var metadata = el.GetProperty("metadata");
        var spec = el.TryGetProperty("spec", out var sp) ? sp : default;
        var status = el.TryGetProperty("status", out var st) ? st : default;

        return new KubernetesDeploymentInfo
        {
            Name = metadata.GetProperty("name").GetString()!,
            Namespace = metadata.GetProperty("namespace").GetString()!,
            Replicas = spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("replicas", out var r)
                ? r.GetInt32() : 0,
            ReadyReplicas = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("readyReplicas", out var rr)
                ? rr.GetInt32() : 0,
            AvailableReplicas = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("availableReplicas", out var ar)
                ? ar.GetInt32() : 0,
            Labels = ParseLabels(metadata),
            CreatedAt = metadata.TryGetProperty("creationTimestamp", out var ct)
                ? DateTimeOffset.Parse(ct.GetString()!) : null
        };
    }

    private static IReadOnlyList<KubernetesServiceInfo> ParseServiceList(JsonElement root)
    {
        var list = new List<KubernetesServiceInfo>();
        if (root.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
                list.Add(ParseService(item));
        }

        return list;
    }

    private static KubernetesServiceInfo ParseService(JsonElement el)
    {
        var metadata = el.GetProperty("metadata");
        var spec = el.GetProperty("spec");

        var ports = new List<KubernetesPortInfo>();
        if (spec.TryGetProperty("ports", out var ps))
        {
            foreach (var p in ps.EnumerateArray())
            {
                ports.Add(new KubernetesPortInfo
                {
                    Name = p.TryGetProperty("name", out var pn) ? pn.GetString() : null,
                    Protocol = p.TryGetProperty("protocol", out var pr) ? pr.GetString()! : "TCP",
                    Port = p.GetProperty("port").GetInt32(),
                    TargetPort = p.TryGetProperty("targetPort", out var tp)
                        ? (tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : int.TryParse(tp.GetString(), out var tpv) ? tpv : 0)
                        : 0,
                    NodePort = p.TryGetProperty("nodePort", out var np) ? np.GetInt32() : null
                });
            }
        }

        var selector = new Dictionary<string, string>();
        if (spec.TryGetProperty("selector", out var sel))
        {
            foreach (var kv in sel.EnumerateObject())
                selector[kv.Name] = kv.Value.GetString()!;
        }

        return new KubernetesServiceInfo
        {
            Name = metadata.GetProperty("name").GetString()!,
            Namespace = metadata.GetProperty("namespace").GetString()!,
            Type = spec.TryGetProperty("type", out var svcType) ? svcType.GetString()! : "ClusterIP",
            ClusterIp = spec.TryGetProperty("clusterIP", out var cip) ? cip.GetString() : null,
            ExternalIp = spec.TryGetProperty("externalIPs", out var eips) && eips.GetArrayLength() > 0
                ? eips[0].GetString() : null,
            Ports = ports,
            Selector = selector
        };
    }

    private static IReadOnlyDictionary<string, string> ParseLabels(JsonElement metadata)
    {
        var labels = new Dictionary<string, string>();
        if (metadata.TryGetProperty("labels", out var lbl))
        {
            foreach (var kv in lbl.EnumerateObject())
                labels[kv.Name] = kv.Value.GetString()!;
        }

        return labels;
    }
}
