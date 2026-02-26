namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class PtzCapabilitiesTests
{
    [Fact]
    public void Default_ReturnsExpectedValues()
    {
        var caps = PtzCapabilities.Default;

        caps.CanPan.Should().BeTrue();
        caps.CanTilt.Should().BeTrue();
        caps.CanZoom.Should().BeFalse();
        caps.PanRange.Min.Should().Be(-1.0f);
        caps.PanRange.Max.Should().Be(1.0f);
        caps.TiltRange.Min.Should().Be(-1.0f);
        caps.TiltRange.Max.Should().Be(1.0f);
        caps.ZoomRange.Min.Should().Be(0f);
        caps.ZoomRange.Max.Should().Be(0f);
        caps.SupportsAbsoluteMove.Should().BeFalse();
        caps.SupportsContinuousMove.Should().BeTrue();
        caps.SupportsRelativeMove.Should().BeTrue();
        caps.SupportsPresets.Should().BeTrue();
        caps.MaxPresets.Should().Be(8);
    }
}
