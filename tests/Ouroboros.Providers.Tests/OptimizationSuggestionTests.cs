namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OptimizationSuggestionTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var suggestion = new OptimizationSuggestion("model1", OptimizationType.IncreasePriority, "Good model", 1);

        suggestion.ModelName.Should().Be("model1");
        suggestion.Type.Should().Be(OptimizationType.IncreasePriority);
        suggestion.Reason.Should().Be("Good model");
        suggestion.Priority.Should().Be(1);
    }
}
