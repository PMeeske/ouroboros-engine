namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class InferenceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var premises = new List<string> { "A implies B", "A is true" };
        var inference = new Inference(premises, "B is true", 0.95, "Modus Ponens");

        inference.Premise.Should().HaveCount(2);
        inference.Conclusion.Should().Be("B is true");
        inference.Confidence.Should().Be(0.95);
        inference.Rule.Should().Be("Modus Ponens");
    }

    [Fact]
    public void Rule_DefaultsToNull()
    {
        var inference = new Inference(new List<string> { "P" }, "Q", 0.8);
        inference.Rule.Should().BeNull();
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var premises = new List<string> { "P" };
        var i1 = new Inference(premises, "Q", 0.8, "R");
        var i2 = new Inference(premises, "Q", 0.8, "R");

        i1.Should().Be(i2);
    }
}
