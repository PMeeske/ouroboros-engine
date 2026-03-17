using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class InferenceTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var premises = new List<string> { "A is B", "B is C" };

        // Act
        var inference = new Inference(premises, "A is C", 0.9, "Transitivity");

        // Assert
        inference.Premise.Should().HaveCount(2);
        inference.Premise[0].Should().Be("A is B");
        inference.Premise[1].Should().Be("B is C");
        inference.Conclusion.Should().Be("A is C");
        inference.Confidence.Should().Be(0.9);
        inference.Rule.Should().Be("Transitivity");
    }

    [Fact]
    public void Constructor_WithoutRule_RuleIsNull()
    {
        // Arrange
        var premises = new List<string> { "A is B" };

        // Act
        var inference = new Inference(premises, "A is B", 0.8);

        // Assert
        inference.Rule.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyPremises_SetsEmptyList()
    {
        // Arrange & Act
        var inference = new Inference(new List<string>(), "Conclusion", 0.5);

        // Assert
        inference.Premise.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var premises = new List<string> { "A is B" };
        var inf1 = new Inference(premises, "A is B", 0.9, "Rule1");
        var inf2 = new Inference(premises, "A is B", 0.9, "Rule1");

        // Act & Assert
        inf1.Should().Be(inf2);
    }

    [Fact]
    public void Confidence_WithZero_IsValid()
    {
        // Arrange & Act
        var inference = new Inference(new List<string>(), "Conclusion", 0.0);

        // Assert
        inference.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Confidence_WithOne_IsValid()
    {
        // Arrange & Act
        var inference = new Inference(new List<string>(), "Conclusion", 1.0);

        // Assert
        inference.Confidence.Should().Be(1.0);
    }
}
