// <copyright file="KubernetesMcpClientOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Kubernetes;

/// <summary>
/// Configuration options for the Kubernetes MCP client.
/// </summary>
public sealed record KubernetesMcpClientOptions
{
    /// <summary>
    /// Gets the Kubernetes API server base URL (e.g. "https://localhost:6443").
    /// If not set, auto-detects in-cluster config from KUBERNETES_SERVICE_HOST.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Gets the bearer token for authentication.
    /// When running in-cluster, reads the service account token automatically.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Gets the path to the kubeconfig file.
    /// Falls back to ~/.kube/config when BaseUrl and Token are not set.
    /// </summary>
    public string? KubeConfigPath { get; init; }

    /// <summary>
    /// Gets the kubeconfig context to use. If null, uses the current context.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets a value indicating whether to skip TLS certificate validation.
    /// Only use for development/testing.
    /// </summary>
    public bool SkipTlsVerify { get; init; }

    /// <summary>
    /// Gets the timeout for HTTP requests (default: 30 seconds).
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the maximum number of retry attempts (default: 3).
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Validates the options. At least one auth method must be available.
    /// </summary>
    /// <returns>True if valid.</returns>
    public bool IsValid()
    {
        // In-cluster: env vars are set automatically
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
            return true;

        // Explicit API server + token
        if (!string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token))
            return true;

        // Kubeconfig path
        if (!string.IsNullOrWhiteSpace(KubeConfigPath))
            return true;

        // Default kubeconfig
        var defaultConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kube", "config");
        return File.Exists(defaultConfig);
    }
}
