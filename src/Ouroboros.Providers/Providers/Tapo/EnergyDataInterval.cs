using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Interval type for energy data queries.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnergyDataInterval
{
    /// <summary>Hourly data.</summary>
    Hourly,
    
    /// <summary>Daily data.</summary>
    Daily,
    
    /// <summary>Monthly data.</summary>
    Monthly
}