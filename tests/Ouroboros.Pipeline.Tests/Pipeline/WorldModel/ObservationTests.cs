namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class ObservationTests
{
    [Fact]
    public void Create_SetsValueAndClampsConfidence()
    {
        var obs = Observation.Create("hello", 0.8);

        obs.Value.Should().Be("hello");
        obs.Confidence.Should().Be(0.8);
    }

    [Fact]
    public void Create_ClampsConfidence()
    {
        Observation.Create("a", 1.5).Confidence.Should().Be(1.0);
        Observation.Create("a", -0.5).Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Certain_HasFullConfidence()
    {
        var obs = Observation.Certain("value");

        obs.Confidence.Should().Be(1.0);
        obs.Value.Should().Be("value");
    }

    [Fact]
    public void GetValueAs_ReturnsSomeWhenCastSucceeds()
    {
        var obs = Observation.Certain("hello");
        var result = obs.GetValueAs<string>();

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetValueAs_ReturnsNoneWhenCastFails()
    {
        var obs = Observation.Certain("hello");
        var result = obs.GetValueAs<int>();

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Create_ThrowsOnNullValue()
    {
        var act = () => Observation.Create(null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }
}
