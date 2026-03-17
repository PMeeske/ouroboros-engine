using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class ReasoningChainStepTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var entities = new List<string> { "e1", "e2" };

        // Act
        var step = new ReasoningChainStep(1, "Traverse", "Following WorksFor relationship", entities);

        // Assert
        step.StepNumber.Should().Be(1);
        step.Operation.Should().Be("Traverse");
        step.Description.Should().Be("Following WorksFor relationship");
        step.EntitiesInvolved.Should().HaveCount(2);
        step.EntitiesInvolved[0].Should().Be("e1");
        step.EntitiesInvolved[1].Should().Be("e2");
    }

    [Fact]
    public void Constructor_WithEmptyEntities_SetsEmptyList()
    {
        // Arrange & Act
        var step = new ReasoningChainStep(1, "Match", "Pattern matching", new List<string>());

        // Assert
        step.EntitiesInvolved.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var entities = new List<string> { "e1" };
        var step1 = new ReasoningChainStep(1, "Traverse", "Description", entities);
        var step2 = new ReasoningChainStep(1, "Traverse", "Description", entities);

        // Act & Assert
        step1.Should().Be(step2);
    }

    [Fact]
    public void RecordEquality_WithDifferentStepNumbers_AreNotEqual()
    {
        // Arrange
        var entities = new List<string> { "e1" };
        var step1 = new ReasoningChainStep(1, "Traverse", "Description", entities);
        var step2 = new ReasoningChainStep(2, "Traverse", "Description", entities);

        // Act & Assert
        step1.Should().NotBe(step2);
    }
}
