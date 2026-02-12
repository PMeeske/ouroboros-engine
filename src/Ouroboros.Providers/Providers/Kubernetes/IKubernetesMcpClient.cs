// <copyright file="IKubernetesMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Kubernetes;

/// <summary>
/// Interface for Kubernetes MCP client operations.
/// Provides methods for managing pods, deployments, services, and reading logs.
/// </summary>
public interface IKubernetesMcpClient
{
    /// <summary>
    /// Lists pods in the specified namespace.
    /// </summary>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="labelSelector">Optional label selector to filter pods.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of pod info or error.</returns>
    Task<Result<IReadOnlyList<KubernetesPodInfo>, string>> ListPodsAsync(
        string ns = "default",
        string? labelSelector = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets details of a specific pod.
    /// </summary>
    /// <param name="name">The pod name.</param>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing pod details or error.</returns>
    Task<Result<KubernetesPodInfo, string>> GetPodAsync(
        string name,
        string ns = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Reads log output from a pod.
    /// </summary>
    /// <param name="podName">The pod name.</param>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="container">Optional container name (for multi-container pods).</param>
    /// <param name="tailLines">Number of most recent lines to return (default: 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing log text or error.</returns>
    Task<Result<string, string>> GetPodLogsAsync(
        string podName,
        string ns = "default",
        string? container = null,
        int tailLines = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Lists deployments in the specified namespace.
    /// </summary>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="labelSelector">Optional label selector.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of deployments or error.</returns>
    Task<Result<IReadOnlyList<KubernetesDeploymentInfo>, string>> ListDeploymentsAsync(
        string ns = "default",
        string? labelSelector = null,
        CancellationToken ct = default);

    /// <summary>
    /// Scales a deployment to the specified replica count.
    /// </summary>
    /// <param name="deploymentName">The deployment name.</param>
    /// <param name="replicas">Desired replica count.</param>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated deployment info or error.</returns>
    Task<Result<KubernetesDeploymentInfo, string>> ScaleDeploymentAsync(
        string deploymentName,
        int replicas,
        string ns = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Lists services in the specified namespace.
    /// </summary>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of services or error.</returns>
    Task<Result<IReadOnlyList<KubernetesServiceInfo>, string>> ListServicesAsync(
        string ns = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Lists namespaces in the cluster.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of namespace names or error.</returns>
    Task<Result<IReadOnlyList<string>, string>> ListNamespacesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Applies a YAML/JSON manifest to the cluster.
    /// </summary>
    /// <param name="manifest">The manifest content (JSON or YAML).</param>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the resource name created/updated or error.</returns>
    Task<Result<string, string>> ApplyManifestAsync(
        string manifest,
        string ns = "default",
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a resource by kind and name.
    /// </summary>
    /// <param name="kind">The resource kind (e.g., "pod", "deployment", "service").</param>
    /// <param name="name">The resource name.</param>
    /// <param name="ns">The namespace (default: "default").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error.</returns>
    Task<Result<string, string>> DeleteResourceAsync(
        string kind,
        string name,
        string ns = "default",
        CancellationToken ct = default);
}
