using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class TapoCameraFrameTests
{
    [Fact]
    public void TapoCameraFrame_Construction_ShouldSetAllProperties()
    {
        // Arrange
        var data = new byte[] { 0xFF, 0xD8, 0x00, 0xFF, 0xD9 };
        var timestamp = new DateTime(2026, 3, 17, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var frame = new TapoCameraFrame(data, 1920, 1080, 42, timestamp, "test-camera");

        // Assert
        frame.Data.Should().BeEquivalentTo(data);
        frame.Width.Should().Be(1920);
        frame.Height.Should().Be(1080);
        frame.FrameNumber.Should().Be(42);
        frame.Timestamp.Should().Be(timestamp);
        frame.CameraName.Should().Be("test-camera");
    }

    [Fact]
    public void TapoCameraFrame_EmptyData_ShouldBeAllowed()
    {
        // Arrange & Act
        var frame = new TapoCameraFrame(
            Array.Empty<byte>(), 0, 0, 0, DateTime.UtcNow, "empty-cam");

        // Assert
        frame.Data.Should().BeEmpty();
    }

    [Fact]
    public void TapoCameraFrame_Equality_SameValues_ShouldNotBeEqualDueToArrayReference()
    {
        // Arrange - byte arrays use reference equality in records
        var timestamp = DateTime.UtcNow;
        var a = new TapoCameraFrame(new byte[] { 1, 2, 3 }, 640, 480, 1, timestamp, "cam");
        var b = new TapoCameraFrame(new byte[] { 1, 2, 3 }, 640, 480, 1, timestamp, "cam");

        // Act & Assert
        // Record equality for arrays uses reference equality, not value equality
        a.Should().NotBe(b);
    }

    [Fact]
    public void TapoCameraFrame_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TapoCameraFrame(
            new byte[] { 1, 2, 3 }, 1280, 720, 1, DateTime.UtcNow, "cam1");

        // Act
        var modified = original with { FrameNumber = 2, CameraName = "cam2" };

        // Assert
        modified.FrameNumber.Should().Be(2);
        modified.CameraName.Should().Be("cam2");
        modified.Width.Should().Be(1280);
        original.FrameNumber.Should().Be(1);
    }

    [Theory]
    [InlineData(640, 360)]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    public void TapoCameraFrame_ShouldAcceptVariousResolutions(int width, int height)
    {
        // Arrange & Act
        var frame = new TapoCameraFrame(
            new byte[] { 0xFF }, width, height, 1, DateTime.UtcNow, "test");

        // Assert
        frame.Width.Should().Be(width);
        frame.Height.Should().Be(height);
    }
}
