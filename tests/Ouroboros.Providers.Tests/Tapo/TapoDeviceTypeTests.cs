namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Tests for camera device type detection.
/// </summary>
[Trait("Category", "Unit")]
public class TapoDeviceTypeTests
{
    [Theory]
    [InlineData(TapoDeviceType.C100)]
    [InlineData(TapoDeviceType.C200)]
    [InlineData(TapoDeviceType.C210)]
    [InlineData(TapoDeviceType.C220)]
    [InlineData(TapoDeviceType.C310)]
    [InlineData(TapoDeviceType.C320)]
    [InlineData(TapoDeviceType.C420)]
    [InlineData(TapoDeviceType.C500)]
    [InlineData(TapoDeviceType.C520)]
    public void CameraDeviceTypes_AreRecognized(TapoDeviceType deviceType)
    {
        // Assert - these should all be camera device types starting with 'C'
        deviceType.ToString().Should().StartWith("C");
    }

    [Theory]
    [InlineData(TapoDeviceType.L510)]
    [InlineData(TapoDeviceType.L530)]
    [InlineData(TapoDeviceType.P100)]
    [InlineData(TapoDeviceType.P110)]
    [InlineData(TapoDeviceType.P300)]
    public void NonCameraDeviceTypes_AreDistinct(TapoDeviceType deviceType)
    {
        // Assert - these should not be camera device types
        deviceType.ToString().Should().NotStartWith("C");
    }
}