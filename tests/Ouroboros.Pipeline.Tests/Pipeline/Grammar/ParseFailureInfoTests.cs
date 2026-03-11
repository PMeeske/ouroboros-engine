namespace Ouroboros.Tests.Pipeline.Grammar;

using Ouroboros.Pipeline.Grammar;

[Trait("Category", "Unit")]
public class ParseFailureInfoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var sut = new ParseFailureInfo("unexpected", "IDENTIFIER", 10, 5, "if x =");

        // Assert
        sut.OffendingToken.Should().Be("unexpected");
        sut.ExpectedTokens.Should().Be("IDENTIFIER");
        sut.Line.Should().Be(10);
        sut.Column.Should().Be(5);
        sut.InputSnippet.Should().Be("if x =");
    }

    [Fact]
    public void Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var a = new ParseFailureInfo("tok", "expected", 1, 2, "snippet");
        var b = new ParseFailureInfo("tok", "expected", 1, 2, "snippet");

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var a = new ParseFailureInfo("tok", "expected", 1, 2, "snippet");
        var b = new ParseFailureInfo("other", "expected", 1, 2, "snippet");

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new ParseFailureInfo("tok", "expected", 1, 2, "snippet");

        // Act
        var modified = original with { Line = 99 };

        // Assert
        modified.Line.Should().Be(99);
        modified.OffendingToken.Should().Be("tok");
        original.Line.Should().Be(1);
    }
}
