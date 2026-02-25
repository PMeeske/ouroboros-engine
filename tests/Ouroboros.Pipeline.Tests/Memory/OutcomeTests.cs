namespace Ouroboros.Tests.Pipeline.Memory;

/// <summary>
/// Unit tests for Outcome record type.
/// </summary>
[Trait("Category", "Unit")]
public class OutcomeTests
{
    [Fact]
    public void Outcome_Successful_ShouldCreateSuccessfulOutcome()
    {
        // Act
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(5));

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Output.Should().Be("output");
        outcome.Duration.Should().Be(TimeSpan.FromSeconds(5));
        outcome.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Outcome_Failed_ShouldCreateFailedOutcome()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var outcome = Outcome.Failed("partial output", TimeSpan.FromSeconds(3), errors);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Output.Should().Be("partial output");
        outcome.Duration.Should().Be(TimeSpan.FromSeconds(3));
        outcome.Errors.Should().HaveCount(2);
        outcome.Errors.Should().Contain("Error 1");
    }

    [Fact]
    public void Outcome_ShouldBeImmutable()
    {
        // Arrange
        var outcome1 = Outcome.Successful("output", TimeSpan.FromSeconds(1));

        // Act
        var outcome2 = outcome1 with { Success = false };

        // Assert
        outcome1.Success.Should().BeTrue();
        outcome2.Success.Should().BeFalse();
    }
}