namespace Ouroboros.Tests.Tapo;

[Trait("Category", "Unit")]
public sealed class PtzMoveResultTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var result = new PtzMoveResult(true, "pan_left", TimeSpan.FromMilliseconds(500), "Moved left");

        result.Success.Should().BeTrue();
        result.Direction.Should().Be("pan_left");
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(500));
        result.Message.Should().Be("Moved left");
    }

    [Fact]
    public void Ctor_MessageDefaultsToNull()
    {
        var result = new PtzMoveResult(false, "stop", TimeSpan.Zero);

        result.Message.Should().BeNull();
    }
}
