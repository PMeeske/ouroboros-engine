using CoreJson = Ouroboros.Core.Json.JsonDefaults;

namespace Ouroboros.Providers.Json;

/// <summary>
/// Layer-local façade over <see cref="Ouroboros.Core.Json.JsonDefaults"/>.
/// All properties delegate to the single canonical set of pre-allocated
/// <see cref="JsonSerializerOptions"/> instances defined in <c>Ouroboros.Core</c>.
/// </summary>
internal static class JsonDefaults
{
    /// <inheritdoc cref="CoreJson.Indented"/>
    public static readonly JsonSerializerOptions Indented = CoreJson.Indented;

    /// <summary>
    /// Compact camelCase output with case-insensitive deserialization.
    /// Standard options for REST API clients (Docker, Firecrawl, Kubernetes).
    /// Delegates to <see cref="CoreJson.CamelCaseInsensitive"/>.
    /// </summary>
    public static readonly JsonSerializerOptions CamelCase = CoreJson.CamelCaseInsensitive;

    /// <inheritdoc cref="CoreJson.Default"/>
    public static readonly JsonSerializerOptions Default = CoreJson.Default;
}
