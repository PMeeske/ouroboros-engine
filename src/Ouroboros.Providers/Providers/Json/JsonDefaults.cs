using System.Text.Json;

namespace Ouroboros.Providers.Json;

/// <summary>
/// Shared, pre-allocated <see cref="JsonSerializerOptions"/> instances
/// to avoid repeated allocations across the providers layer.
/// </summary>
internal static class JsonDefaults
{
    /// <summary>
    /// Indented output with camelCase property names.
    /// </summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Compact camelCase output with case-insensitive deserialization.
    /// Standard options for REST API clients (Docker, Firecrawl, Kubernetes).
    /// </summary>
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Case-insensitive property name matching, default output.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
