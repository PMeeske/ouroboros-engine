using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class PtzCapabilitiesTests
{
    [Fact]
    public void PtzCapabilities_Construction_ShouldSetAllProperties()
    {
        // Arrange & Act
        var capabilities = new PtzCapabilities(
            CanPan: true,
            CanTilt: false,
            CanZoom: true,
            PanRange: (-0.5f, 0.5f),
            TiltRange: (-0.3f, 0.3f),
            ZoomRange: (1f, 10f),
            SupportsAbsoluteMove: true,
            SupportsContinuousMove: false,
            SupportsRelativeMove: true,
            SupportsPresets: true,
            MaxPresets: 16);

        // Assert
        capabilities.CanPan.Should().BeTrue();
        capabilities.CanTilt.Should().BeFalse();
        capabilities.CanZoom.Should().BeTrue();
        capabilities.PanRange.Should().Be((-0.5f, 0.5f));
        capabilities.TiltRange.Should().Be((-0.3f, 0.3f));
        capabilities.ZoomRange.Should().Be((1f, 10f));
        capabilities.SupportsAbsoluteMove.Should().BeTrue();
        capabilities.SupportsContinuousMove.Should().BeFalse();
        capabilities.SupportsRelativeMove.Should().BeTrue();
        capabilities.SupportsPresets.Should().BeTrue();
        capabilities.MaxPresets.Should().Be(16);
    }

    [Fact]
    public void PtzCapabilities_Default_ShouldReturnC200Defaults()
    {
        // Act
        var defaults = PtzCapabilities.Default;

        // Assert
        defaults.CanPan.Should().BeTrue();
        defaults.CanTilt.Should().BeTrue();
        defaults.CanZoom.Should().BeFalse();
        defaults.PanRange.Should().Be((-1.0f, 1.0f));
        defaults.TiltRange.Should().Be((-1.0f, 1.0f));
        defaults.ZoomRange.Should().Be((0f, 0f));
        defaults.SupportsAbsoluteMove.Should().BeFalse();
        defaults.SupportsContinuousMove.Should().BeTrue();
        defaults.SupportsRelativeMove.Should().BeTrue();
        defaults.SupportsPresets.Should().BeTrue();
        defaults.MaxPresets.Should().Be(8);
    }

    [Fact]
    public void PtzCapabilities_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var a = PtzCapabilities.Default;
        var b = PtzCapabilities.Default;

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void PtzCapabilities_Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var a = PtzCapabilities.Default;
        var b = a with { CanZoom = true };

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void PtzCapabilities_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = PtzCapabilities.Default;

        // Act
        var modified = original with { MaxPresets = 32 };

        // Assert
        modified.MaxPresets.Should().Be(32);
        original.MaxPresets.Should().Be(8);
    }
}
