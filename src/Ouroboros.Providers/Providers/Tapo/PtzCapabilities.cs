namespace Ouroboros.Providers.Tapo;

/// <summary>
/// PTZ capabilities of a camera.
/// </summary>
/// <param name="CanPan">Whether the camera can pan (horizontal rotation).</param>
/// <param name="CanTilt">Whether the camera can tilt (vertical rotation).</param>
/// <param name="CanZoom">Whether the camera has motorized zoom.</param>
/// <param name="PanRange">Pan speed range (min, max).</param>
/// <param name="TiltRange">Tilt speed range (min, max).</param>
/// <param name="ZoomRange">Zoom range (min, max).</param>
/// <param name="SupportsAbsoluteMove">Whether absolute positioning is supported.</param>
/// <param name="SupportsContinuousMove">Whether continuous movement is supported.</param>
/// <param name="SupportsRelativeMove">Whether relative movement is supported.</param>
/// <param name="SupportsPresets">Whether position presets are supported.</param>
/// <param name="MaxPresets">Maximum number of presets.</param>
public sealed record PtzCapabilities(
    bool CanPan,
    bool CanTilt,
    bool CanZoom,
    (float Min, float Max) PanRange,
    (float Min, float Max) TiltRange,
    (float Min, float Max) ZoomRange,
    bool SupportsAbsoluteMove,
    bool SupportsContinuousMove,
    bool SupportsRelativeMove,
    bool SupportsPresets,
    int MaxPresets)
{
    /// <summary>
    /// Default capabilities for a Tapo C200 camera.
    /// </summary>
    public static PtzCapabilities Default => new(
        CanPan: true,
        CanTilt: true,
        CanZoom: false,
        PanRange: (-1.0f, 1.0f),
        TiltRange: (-1.0f, 1.0f),
        ZoomRange: (0f, 0f),
        SupportsAbsoluteMove: false,
        SupportsContinuousMove: true,
        SupportsRelativeMove: true,
        SupportsPresets: true,
        MaxPresets: 8);
}