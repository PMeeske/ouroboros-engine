namespace Ouroboros.Tests.Pipeline.Grammar;

using Ouroboros.Pipeline.Grammar;

[Trait("Category", "Unit")]
public class GrammarRefinementResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange & Act
        var sut = new GrammarRefinementResult(true, "grammar rule;", "Fixed token mismatch");

        // Assert
        sut.Success.Should().BeTrue();
        sut.RefinedGrammarG4.Should().Be("grammar rule;");
        sut.Explanation.Should().Be("Fixed token mismatch");
    }

    [Fact]
    public void Constructor_SuccessFalse_SetsProperty()
    {
        // Arrange & Act
        var sut = new GrammarRefinementResult(false, "", "Could not refine");

        // Assert
        sut.Success.Should().BeFalse();
    }

    [Fact]
    public void Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var a = new GrammarRefinementResult(true, "g4", "reason");
        var b = new GrammarRefinementResult(true, "g4", "reason");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new GrammarRefinementResult(true, "g4", "reason");
        var b = new GrammarRefinementResult(false, "g4", "reason");

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new GrammarRefinementResult(true, "g4", "reason");

        // Act
        var modified = original with { Explanation = "new reason" };

        // Assert
        modified.Explanation.Should().Be("new reason");
        modified.Success.Should().BeTrue();
        original.Explanation.Should().Be("reason");
    }
}
