// <copyright file="IDockerMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Providers.Docker;

/// <summary>
/// Interface for Docker Engine MCP client operations.
/// Provides methods for managing containers, images, volumes, and networks.
/// </summary>
public interface IDockerMcpClient
{
    /// <summary>
    /// Lists running containers (optionally all).
    /// </summary>
    /// <param name="all">If true, include stopped containers.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of container info or error.</returns>
    Task<Result<IReadOnlyList<DockerContainerInfo>, string>> ListContainersAsync(
        bool all = false,
        CancellationToken ct = default);

    /// <summary>
    /// Gets details of a specific container.
    /// </summary>
    /// <param name="containerId">Container ID or name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing container details or error.</returns>
    Task<Result<DockerContainerInfo, string>> InspectContainerAsync(
        string containerId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets log output from a container.
    /// </summary>
    /// <param name="containerId">Container ID or name.</param>
    /// <param name="tail">Number of lines from the end (default: 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing log text or error.</returns>
    Task<Result<string, string>> GetContainerLogsAsync(
        string containerId,
        int tail = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Starts a stopped container.
    /// </summary>
    /// <param name="containerId">Container ID or name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error.</returns>
    Task<Result<string, string>> StartContainerAsync(
        string containerId,
        CancellationToken ct = default);

    /// <summary>
    /// Stops a running container.
    /// </summary>
    /// <param name="containerId">Container ID or name.</param>
    /// <param name="timeoutSeconds">Seconds to wait before killing (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error.</returns>
    Task<Result<string, string>> StopContainerAsync(
        string containerId,
        int timeoutSeconds = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a stopped container.
    /// </summary>
    /// <param name="containerId">Container ID or name.</param>
    /// <param name="force">Force removal of a running container.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error.</returns>
    Task<Result<string, string>> RemoveContainerAsync(
        string containerId,
        bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Lists Docker images on the host.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of image info or error.</returns>
    Task<Result<IReadOnlyList<DockerImageInfo>, string>> ListImagesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Pulls an image from a registry.
    /// </summary>
    /// <param name="image">Image reference (e.g., "nginx:latest").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error.</returns>
    Task<Result<string, string>> PullImageAsync(
        string image,
        CancellationToken ct = default);

    /// <summary>
    /// Lists Docker networks.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of network info or error.</returns>
    Task<Result<IReadOnlyList<DockerNetworkInfo>, string>> ListNetworksAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Lists Docker volumes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of volume info or error.</returns>
    Task<Result<IReadOnlyList<DockerVolumeInfo>, string>> ListVolumesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Creates and starts a new container from an image.
    /// </summary>
    /// <param name="image">The image to use.</param>
    /// <param name="name">Optional container name.</param>
    /// <param name="ports">Port mappings (host:container), e.g. ["8080:80"].</param>
    /// <param name="envVars">Environment variables (KEY=VALUE).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the container ID or error.</returns>
    Task<Result<string, string>> RunContainerAsync(
        string image,
        string? name = null,
        IReadOnlyList<string>? ports = null,
        IReadOnlyList<string>? envVars = null,
        CancellationToken ct = default);
}
