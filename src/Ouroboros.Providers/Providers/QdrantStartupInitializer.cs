using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.Configuration;

namespace Ouroboros.Providers;

/// <summary>
/// Hosted service that discovers Qdrant collections on startup and
/// populates the <see cref="IQdrantCollectionRegistry"/> with live mappings.
/// </summary>
public sealed class QdrantStartupInitializer : IHostedService
{
    private readonly IQdrantCollectionRegistry _registry;
    private readonly ILogger<QdrantStartupInitializer>? _logger;

    public QdrantStartupInitializer(
        IQdrantCollectionRegistry registry,
        ILogger<QdrantStartupInitializer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting Qdrant collection discovery...");
        await _registry.DiscoverAsync(cancellationToken);
        _logger?.LogInformation(
            "Qdrant startup complete: {Count} collection mappings active",
            _registry.GetAllMappings().Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
