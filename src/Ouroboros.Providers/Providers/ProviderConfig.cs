namespace Ouroboros.Providers;

/// <summary>
/// Configuration for a single provider in the round-robin pool.
/// </summary>
public sealed record ProviderConfig(
    string Name,
    ChatEndpointType EndpointType,
    string? Endpoint = null,
    string? ApiKey = null,
    string? Model = null,
    int Weight = 1,
    bool Enabled = true);