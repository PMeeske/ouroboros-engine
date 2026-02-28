// <copyright file="KubernetesMcpClient.Helpers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text.Json;

namespace Ouroboros.Providers.Kubernetes;

/// <summary>
/// Helper methods for KubernetesMcpClient: HTTP client creation, URL building, and JSON parsing.
/// </summary>
public sealed partial class KubernetesMcpClient
{
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
