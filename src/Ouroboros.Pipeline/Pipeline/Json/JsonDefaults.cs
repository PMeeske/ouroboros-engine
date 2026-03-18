using System.Text.Json;
using CoreJson = Ouroboros.Core.Json.JsonDefaults;

namespace Ouroboros.Pipeline.Json;

/// <summary>
/// Layer-local façade over <see cref="Ouroboros.Core.Json.JsonDefaults"/>.
/// All properties delegate to the single canonical set of pre-allocated
/// <see cref="JsonSerializerOptions"/> instances defined in <c>Ouroboros.Core</c>.
/// </summary>
internal static class JsonDefaults
{
    /// <inheritdoc cref="CoreJson.Indented"/>
    public static readonly JsonSerializerOptions Indented = CoreJson.Indented;

    /// <inheritdoc cref="CoreJson.CamelCase"/>
    public static readonly JsonSerializerOptions CamelCase = CoreJson.CamelCase;

    /// <inheritdoc cref="CoreJson.Default"/>
    public static readonly JsonSerializerOptions Default = CoreJson.Default;
}
