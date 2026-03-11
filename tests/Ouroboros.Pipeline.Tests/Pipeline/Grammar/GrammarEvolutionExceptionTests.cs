namespace Ouroboros.Tests.Pipeline.Grammar;

using Ouroboros.Pipeline.Grammar;

[Trait("Category", "Unit")]
public class GrammarEvolutionExceptionTests
{
    [Fact]
    public void Constructor_SetsMessageDescriptionAndAttempts()
    {
        // Arrange & Act
        var ex = new GrammarEvolutionException("Failed to converge", "IF-THEN rules", 3);

        // Assert
        ex.Message.Should().Be("Failed to converge");
        ex.Description.Should().Be("IF-THEN rules");
        ex.Attempts.Should().Be(3);
    }

    [Fact]
    public void InheritsFromException()
    {
        // Arrange & Act
        var ex = new GrammarEvolutionException("msg", "desc", 1);

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Constructor_ZeroAttempts_SetsProperty()
    {
        // Arrange & Act
        var ex = new GrammarEvolutionException("msg", "desc", 0);

        // Assert
        ex.Attempts.Should().Be(0);
    }

    [Fact]
    public void Constructor_NegativeAttempts_SetsProperty()
    {
        // Arrange & Act
        var ex = new GrammarEvolutionException("msg", "desc", -1);

        // Assert
        ex.Attempts.Should().Be(-1);
    }
}
