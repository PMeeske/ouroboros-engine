using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class VariantResultTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var promptResults = new List<PromptResult>
        {
            new("Test prompt", true, 150.0, 0.95, "gpt-4", null)
        };
        var metrics = new VariantMetrics(0.9, 150.0, 200.0, 250.0, 0.95, 10, 9);

        // Act
        var sut = new VariantResult("variant-A", promptResults, metrics);

        // Assert
        sut.VariantId.Should().Be("variant-A");
        sut.PromptResults.Should().HaveCount(1);
        sut.Metrics.Should().Be(metrics);
    }

    [Fact]
    public void Constructor_WithEmptyPromptResults_ShouldWork()
    {
        // Arrange
        var metrics = new VariantMetrics(0.0, 0.0, 0.0, 0.0, 0.0, 0, 0);

        // Act
        var sut = new VariantResult("variant-empty", new List<PromptResult>(), metrics);

        // Assert
        sut.PromptResults.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var results = new List<PromptResult>();
        var metrics = new VariantMetrics(1.0, 100.0, 150.0, 200.0, 0.99, 5, 5);
        var a = new VariantResult("v1", results, metrics);
        var b = new VariantResult("v1", results, metrics);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var metrics = new VariantMetrics(0.5, 100.0, 150.0, 200.0, 0.8, 10, 5);
        var original = new VariantResult("v1", new List<PromptResult>(), metrics);

        // Act
        var modified = original with { VariantId = "v2" };

        // Assert
        modified.VariantId.Should().Be("v2");
        modified.Metrics.Should().Be(metrics);
    }
}
