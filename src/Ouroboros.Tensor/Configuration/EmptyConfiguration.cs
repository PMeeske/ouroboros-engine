// <copyright file="EmptyConfiguration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Ouroboros.Tensor.Configuration;

/// <summary>
/// Minimal null-object <see cref="IConfiguration"/> — every key returns null,
/// every section is empty. Used by
/// <c>GpuOrchestrationExtensions.AddOuroborosTensorGpu</c> as the default
/// configuration when the host has not registered one, so
/// <c>DxgiVramLayoutProvider</c> falls through to auto-detect without a
/// <see cref="Microsoft.Extensions.Configuration"/> package dependency.
/// </summary>
internal sealed class EmptyConfiguration : IConfiguration
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly EmptyConfiguration Instance = new();

    private EmptyConfiguration()
    {
    }

    /// <inheritdoc/>
    public string? this[string key]
    {
        get => null;
        set => throw new InvalidOperationException("EmptyConfiguration is read-only.");
    }

    /// <inheritdoc/>
    public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

    /// <inheritdoc/>
    public IChangeToken GetReloadToken() => NullChangeToken.Instance;

    /// <inheritdoc/>
    public IConfigurationSection GetSection(string key) => new EmptyConfigurationSection(key);

    private sealed class NullChangeToken : IChangeToken
    {
        public static readonly NullChangeToken Instance = new();

        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class EmptyConfigurationSection : IConfigurationSection
    {
        public EmptyConfigurationSection(string key)
        {
            Key = key;
            Path = key;
        }

        public string Key { get; }

        public string Path { get; }

        public string? Value { get => null; set => throw new InvalidOperationException("EmptyConfiguration is read-only."); }

        public string? this[string key]
        {
            get => null;
            set => throw new InvalidOperationException("EmptyConfiguration is read-only.");
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken() => NullChangeToken.Instance;

        public IConfigurationSection GetSection(string key) => new EmptyConfigurationSection(Path + ":" + key);
    }
}
