using System.Text.Json;

namespace Ouroboros.Pipeline.Json;

/// <summary>
/// Shared, pre-allocated <see cref="JsonSerializerOptions"/> instances
/// to avoid repeated allocations across the pipeline layer.
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
    /// Compact output with camelCase property names.
    /// </summary>
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Case-insensitive property name matching, default output.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
