namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class PredictedEffectTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var node = CausalNode.CreateEvent("alarm", "desc");
        var effect = new PredictedEffect(node, 0.75, 0.6);

        effect.Node.Should().Be(node);
        effect.Probability.Should().Be(0.75);
        effect.PathStrength.Should().Be(0.6);
    }
}
