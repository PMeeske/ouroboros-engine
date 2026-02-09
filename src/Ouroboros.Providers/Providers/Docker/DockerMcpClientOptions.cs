// <copyright file="DockerMcpClientOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Docker;

/// <summary>
/// Configuration options for the Docker Engine MCP client.
/// </summary>
public sealed record DockerMcpClientOptions
{
    /// <summary>
    /// Gets the Docker Engine API base URL.
    /// Linux default: "http://localhost/v1.43" via unix socket.
    /// Windows default: "http://localhost:2375" or npipe.
    /// Explicit TCP: "http://host:2375" or "https://host:2376".
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Gets the Unix socket path (Linux/macOS only).
    /// Default: /var/run/docker.sock.
    /// </summary>
    public string SocketPath { get; init; } = "/var/run/docker.sock";

    /// <summary>
    /// Gets the named pipe path (Windows only).
    /// Default: //./pipe/docker_engine.
    /// </summary>
    public string PipePath { get; init; } = @"//./pipe/docker_engine";

    /// <summary>
    /// Gets the Docker Engine API version (default: "v1.43").
    /// </summary>
    public string ApiVersion { get; init; } = "v1.43";

    /// <summary>
    /// Gets a value indicating whether to use TLS for TCP connections.
    /// </summary>
    public bool UseTls { get; init; }

    /// <summary>
    /// Gets the path to the TLS certificate directory (ca.pem, cert.pem, key.pem).
    /// </summary>
    public string? TlsCertPath { get; init; }

    /// <summary>
    /// Gets the timeout for HTTP requests (default: 30 seconds).
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the maximum retry attempts (default: 3).
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <returns>True if valid.</returns>
    public bool IsValid()
    {
        // Explicit URL always works
        if (!string.IsNullOrWhiteSpace(BaseUrl))
            return true;

        // On Windows, named pipe is always available if Docker Desktop is running
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            return true;

        // On Unix, check socket exists
        return File.Exists(SocketPath);
    }
}
