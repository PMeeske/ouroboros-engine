namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class ExperienceTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var exp = Experience.Create("s1", "a1", 0.5, "s2");

        exp.State.Should().Be("s1");
        exp.Action.Should().Be("a1");
        exp.Reward.Should().Be(0.5);
        exp.NextState.Should().Be("s2");
        exp.Priority.Should().Be(1.0);
    }

    [Fact]
    public void WithTDErrorPriority_UpdatesPriority()
    {
        var exp = Experience.Create("s1", "a1", 0.5, "s2");
        var updated = exp.WithTDErrorPriority(0.3);

        // Priority = Math.Abs(tdError) + epsilon, where epsilon = 0.01
        updated.Priority.Should().BeApproximately(0.31, 0.001);
        exp.Priority.Should().Be(1.0);
    }

    [Fact]
    public void WithMetadata_AddsMetadataEntry()
    {
        var exp = Experience.Create("s1", "a1", 0.5, "s2");
        var updated = exp.WithMetadata("key", "value");

        updated.Metadata.Should().ContainKey("key");
        exp.Metadata.Should().BeEmpty();
    }
}
