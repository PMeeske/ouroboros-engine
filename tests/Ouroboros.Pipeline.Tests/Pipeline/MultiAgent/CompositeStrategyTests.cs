namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class CompositeStrategyTests
{
    private static readonly DelegationCriteria DefaultCriteria =
        DelegationCriteria.FromGoal(Goal.Atomic("test"));

    [Fact]
    public void Name_IsComposite()
    {
        // Arrange
        var composite = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0));

        // Act & Assert
        composite.Name.Should().Be("Composite");
    }

    [Fact]
    public void Create_WithNull_Throws()
    {
        // Act
        Action act = () => CompositeStrategy.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEmpty_Throws()
    {
        // Act
        Action act = () => CompositeStrategy.Create();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithZeroWeight_Throws()
    {
        // Act
        Action act = () => CompositeStrategy.Create(
            (new RoundRobinStrategy(), 0.0));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithNullStrategy_Throws()
    {
        // Act
        Action act = () => CompositeStrategy.Create(
            (null!, 1.0));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectAgent_EmptyTeam_ReturnsNoMatch()
    {
        // Arrange
        var composite = CompositeStrategy.Create(
            (new RoundRobinStrategy(), 1.0));

        // Act
        var result = composite.SelectAgent(DefaultCriteria, AgentTeam.Empty);

        // Assert
        result.HasMatch.Should().BeFalse();
    }
}
