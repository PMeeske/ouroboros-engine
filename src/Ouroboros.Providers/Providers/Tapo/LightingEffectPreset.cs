using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Preset lighting effects for RGB light strips.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LightingEffectPreset
{
    /// <summary>Aurora effect.</summary>
    Aurora,
    
    /// <summary>Bubbling cauldron effect.</summary>
    BubblingCauldron,
    
    /// <summary>Candy cane effect.</summary>
    CandyCane,
    
    /// <summary>Christmas effect.</summary>
    Christmas,
    
    /// <summary>Flicker effect.</summary>
    Flicker,
    
    /// <summary>Grandma's Christmas lights effect.</summary>
    GrandmasChristmasLights,
    
    /// <summary>Hanukkah effect.</summary>
    Hanukkah,
    
    /// <summary>Haunted mansion effect.</summary>
    HauntedMansion,
    
    /// <summary>Icicle effect.</summary>
    Icicle,
    
    /// <summary>Lightning effect.</summary>
    Lightning,
    
    /// <summary>Ocean effect.</summary>
    Ocean,
    
    /// <summary>Rainbow effect.</summary>
    Rainbow,
    
    /// <summary>Raindrop effect.</summary>
    Raindrop,
    
    /// <summary>Spring effect.</summary>
    Spring,
    
    /// <summary>Sunrise effect.</summary>
    Sunrise,
    
    /// <summary>Sunset effect.</summary>
    Sunset,
    
    /// <summary>Valentines effect.</summary>
    Valentines
}