namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CausalEdgeTests
{
    [Fact]
    public void Create_ClampsStrength()
    {
        var src = Guid.NewGuid();
        var tgt = Guid.NewGuid();
        var edge = CausalEdge.Create(src, tgt, 1.5);

        edge.Strength.Should().Be(1.0);
        edge.Condition.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Create_WithCondition_SetsCondition()
    {
        var edge = CausalEdge.Create(Guid.NewGuid(), Guid.NewGuid(), 0.7, "when hot");

        edge.Condition.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Deterministic_HasStrengthOne()
    {
        var edge = CausalEdge.Deterministic(Guid.NewGuid(), Guid.NewGuid());

        edge.Strength.Should().Be(1.0);
    }
}
