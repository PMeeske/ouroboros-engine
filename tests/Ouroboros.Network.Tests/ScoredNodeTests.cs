namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ScoredNodeTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var node = new MonadNode(
            Guid.NewGuid(), "T", "{}", DateTimeOffset.UtcNow, ImmutableArray<Guid>.Empty);

        var scored = new ScoredNode(node, 0.95f);

        scored.Node.Should().Be(node);
        scored.Score.Should().Be(0.95f);
    }
}
