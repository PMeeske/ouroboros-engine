using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class PredictedEffectTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var node = CausalNode.CreateEvent("crash", "System crash");

        // Act
        var effect = new PredictedEffect(node, 0.7, 0.5);

        // Assert
        effect.Node.Should().Be(node);
        effect.Probability.Should().Be(0.7);
        effect.PathStrength.Should().Be(0.5);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var node = CausalNode.CreateEvent("ev", "Event");
        var e1 = new PredictedEffect(node, 0.5, 0.3);
        var e2 = new PredictedEffect(node, 0.5, 0.3);

        e1.Should().Be(e2);
    }
}
