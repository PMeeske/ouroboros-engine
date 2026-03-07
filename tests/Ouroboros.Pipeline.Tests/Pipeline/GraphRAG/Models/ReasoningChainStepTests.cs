namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class ReasoningChainStepTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var entities = new List<string> { "e1", "e2" };
        var step = new ReasoningChainStep(1, "Traverse", "Following link from e1 to e2", entities);

        step.StepNumber.Should().Be(1);
        step.Operation.Should().Be("Traverse");
        step.Description.Should().Be("Following link from e1 to e2");
        step.EntitiesInvolved.Should().HaveCount(2);
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var entities = new List<string> { "e1" };
        var s1 = new ReasoningChainStep(1, "Op", "Desc", entities);
        var s2 = new ReasoningChainStep(1, "Op", "Desc", entities);

        s1.Should().Be(s2);
    }
}
