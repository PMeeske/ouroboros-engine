// <copyright file="DockerModels.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Docker;

/// <summary>
/// Represents a Docker container.
/// </summary>
public sealed record DockerContainerInfo
{
    /// <summary>Gets the container ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the container short ID (first 12 chars).</summary>
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;

    /// <summary>Gets the container name(s).</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Gets the image used.</summary>
    public required string Image { get; init; }

    /// <summary>Gets the container state (running, exited, created, etc.).</summary>
    public required string State { get; init; }

    /// <summary>Gets the status string (e.g., "Up 3 hours").</summary>
    public string? Status { get; init; }

    /// <summary>Gets the port mappings.</summary>
    public IReadOnlyList<DockerPortMapping> Ports { get; init; } = [];

    /// <summary>Gets the container labels.</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
/// Represents a Docker port mapping.
/// </summary>
public sealed record DockerPortMapping
{
    /// <summary>Gets the host IP.</summary>
    public string? HostIp { get; init; }

    /// <summary>Gets the host port.</summary>
    public int? HostPort { get; init; }

    /// <summary>Gets the container port.</summary>
    public int ContainerPort { get; init; }

    /// <summary>Gets the protocol (tcp/udp).</summary>
    public string Protocol { get; init; } = "tcp";
}

/// <summary>
/// Represents a Docker image.
/// </summary>
public sealed record DockerImageInfo
{
    /// <summary>Gets the image ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the repository tags.</summary>
    public IReadOnlyList<string> RepoTags { get; init; } = [];

    /// <summary>Gets the image size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
/// Represents a Docker network.
/// </summary>
public sealed record DockerNetworkInfo
{
    /// <summary>Gets the network ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the network name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the driver (bridge, overlay, host, etc.).</summary>
    public required string Driver { get; init; }

    /// <summary>Gets the network scope (local, swarm, global).</summary>
    public string? Scope { get; init; }
}

/// <summary>
/// Represents a Docker volume.
/// </summary>
public sealed record DockerVolumeInfo
{
    /// <summary>Gets the volume name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the driver.</summary>
    public required string Driver { get; init; }

    /// <summary>Gets the mountpoint.</summary>
    public string? Mountpoint { get; init; }

    /// <summary>Gets the volume labels.</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
}
