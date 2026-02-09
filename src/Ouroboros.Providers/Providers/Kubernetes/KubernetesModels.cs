// <copyright file="KubernetesModels.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Kubernetes;

/// <summary>
/// Represents a Kubernetes pod.
/// </summary>
public sealed record KubernetesPodInfo
{
    /// <summary>Gets the pod name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the pod phase (Running, Pending, Succeeded, Failed, Unknown).</summary>
    public required string Phase { get; init; }

    /// <summary>Gets the pod IP address.</summary>
    public string? PodIp { get; init; }

    /// <summary>Gets the node the pod is running on.</summary>
    public string? NodeName { get; init; }

    /// <summary>Gets the pod labels.</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the container names.</summary>
    public IReadOnlyList<string> Containers { get; init; } = [];

    /// <summary>Gets the pod creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Gets the restart count across all containers.</summary>
    public int RestartCount { get; init; }
}

/// <summary>
/// Represents a Kubernetes deployment.
/// </summary>
public sealed record KubernetesDeploymentInfo
{
    /// <summary>Gets the deployment name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the desired replica count.</summary>
    public int Replicas { get; init; }

    /// <summary>Gets the number of ready replicas.</summary>
    public int ReadyReplicas { get; init; }

    /// <summary>Gets the number of available replicas.</summary>
    public int AvailableReplicas { get; init; }

    /// <summary>Gets the deployment labels.</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the deployment creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
/// Represents a Kubernetes service.
/// </summary>
public sealed record KubernetesServiceInfo
{
    /// <summary>Gets the service name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the service type (ClusterIP, NodePort, LoadBalancer, ExternalName).</summary>
    public required string Type { get; init; }

    /// <summary>Gets the cluster IP.</summary>
    public string? ClusterIp { get; init; }

    /// <summary>Gets the external IP if available.</summary>
    public string? ExternalIp { get; init; }

    /// <summary>Gets the service ports.</summary>
    public IReadOnlyList<KubernetesPortInfo> Ports { get; init; } = [];

    /// <summary>Gets the service selector labels.</summary>
    public IReadOnlyDictionary<string, string> Selector { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Represents a Kubernetes service port.
/// </summary>
public sealed record KubernetesPortInfo
{
    /// <summary>Gets the port name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the protocol (TCP, UDP).</summary>
    public string Protocol { get; init; } = "TCP";

    /// <summary>Gets the port number.</summary>
    public int Port { get; init; }

    /// <summary>Gets the target port.</summary>
    public int TargetPort { get; init; }

    /// <summary>Gets the node port (for NodePort/LoadBalancer types).</summary>
    public int? NodePort { get; init; }
}
