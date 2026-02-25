using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Enumeration of supported Tapo device types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TapoDeviceType
{
    /// <summary>Light bulb (non-color).</summary>
    L510,
    
    /// <summary>Light bulb (non-color).</summary>
    L520,
    
    /// <summary>Light bulb (non-color).</summary>
    L610,
    
    /// <summary>Light bulb with customizable colors.</summary>
    L530,
    
    /// <summary>Light bulb with customizable colors.</summary>
    L535,
    
    /// <summary>Light bulb with customizable colors.</summary>
    L630,
    
    /// <summary>RGB light strip.</summary>
    L900,
    
    /// <summary>RGB light strip with individually colored segments.</summary>
    L920,
    
    /// <summary>RGB light strip with individually colored segments.</summary>
    L930,
    
    /// <summary>Smart plug.</summary>
    P100,
    
    /// <summary>Smart plug.</summary>
    P105,
    
    /// <summary>Smart plug with energy monitoring.</summary>
    P110,
    
    /// <summary>Smart plug with energy monitoring.</summary>
    P110M,
    
    /// <summary>Smart plug with energy monitoring.</summary>
    P115,
    
    /// <summary>Power strip.</summary>
    P300,
    
    /// <summary>Power strip.</summary>
    P304,
    
    /// <summary>Power strip.</summary>
    P304M,
    
    /// <summary>Power strip.</summary>
    P316,

    // Camera device types for video/audio embodiment

    /// <summary>Pan/Tilt Home Security Wi-Fi Camera.</summary>
    C100,

    /// <summary>Pan/Tilt Home Security Wi-Fi Camera with 1080p.</summary>
    C200,

    /// <summary>Pan/Tilt Home Security Wi-Fi Camera with 2K.</summary>
    C210,

    /// <summary>Pan/Tilt AI Home Security Wi-Fi Camera with 2K QHD.</summary>
    C220,

    /// <summary>Outdoor Security Wi-Fi Camera.</summary>
    C310,

    /// <summary>Outdoor Security Wi-Fi Camera with 2K QHD.</summary>
    C320,

    /// <summary>Smart Wire-Free Indoor/Outdoor Camera.</summary>
    C420,

    /// <summary>Outdoor Pan/Tilt Security Wi-Fi Camera.</summary>
    C500,

    /// <summary>Outdoor Pan/Tilt Security Wi-Fi Camera with 2K QHD.</summary>
    C520
}