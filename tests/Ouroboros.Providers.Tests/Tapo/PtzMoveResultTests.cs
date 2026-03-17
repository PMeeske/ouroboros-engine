using FluentAssertions;

namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public class PtzMoveResultTests
{
    [Fact]
    public void PtzMoveResult_Construction_WithAllParameters_ShouldSetProperties()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(500);

        // Act
        var result = new PtzMoveResult(true, "pan_left", duration, "Moved left");

        // Assert
        result.Success.Should().BeTrue();
        result.Direction.Should().Be("pan_left");
        result.Duration.Should().Be(duration);
        result.Message.Should().Be("Moved left");
    }

    [Fact]
    public void PtzMoveResult_Construction_WithoutMessage_ShouldDefaultToNull()
    {
        // Arrange & Act
        var result = new PtzMoveResult(false, "stop", TimeSpan.Zero);

        // Assert
        result.Message.Should().BeNull();
    }

    [Fact]
    public void PtzMoveResult_Success_ShouldReflectMovementOutcome()
    {
        // Arrange
        var successful = new PtzMoveResult(true, "tilt_up", TimeSpan.FromSeconds(1));
        var failed = new PtzMoveResult(false, "tilt_up", TimeSpan.Zero, "Camera offline");

        // Act & Assert
        successful.Success.Should().BeTrue();
        failed.Success.Should().BeFalse();
        failed.Message.Should().Be("Camera offline");
    }

    [Fact]
    public void PtzMoveResult_Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(250);
        var a = new PtzMoveResult(true, "pan_right", duration, "OK");
        var b = new PtzMoveResult(true, "pan_right", duration, "OK");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void PtzMoveResult_With_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new PtzMoveResult(true, "pan_left", TimeSpan.FromSeconds(1));

        // Act
        var modified = original with { Success = false, Message = "Error occurred" };

        // Assert
        modified.Success.Should().BeFalse();
        modified.Message.Should().Be("Error occurred");
        original.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("pan_left")]
    [InlineData("pan_right")]
    [InlineData("tilt_up")]
    [InlineData("tilt_down")]
    [InlineData("stop")]
    [InlineData("go_home")]
    [InlineData("patrol_sweep")]
    public void PtzMoveResult_Direction_ShouldAcceptVariousValues(string direction)
    {
        // Arrange & Act
        var result = new PtzMoveResult(true, direction, TimeSpan.Zero);

        // Assert
        result.Direction.Should().Be(direction);
    }
}
