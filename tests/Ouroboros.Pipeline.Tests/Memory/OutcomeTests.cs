using FluentAssertions;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class OutcomeTests
{
    [Fact]
    public void Constructor_WithAllProperties_SetsValuesCorrectly()
    {
        // Arrange
        var errors = ImmutableList.Create("error1", "error2");
        var duration = TimeSpan.FromSeconds(5);

        // Act
        var outcome = new Outcome(true, "test output", duration, errors);

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Output.Should().Be("test output");
        outcome.Duration.Should().Be(duration);
        outcome.Errors.Should().HaveCount(2);
        outcome.Errors.Should().Contain("error1");
        outcome.Errors.Should().Contain("error2");
    }

    [Fact]
    public void Successful_CreatesSuccessfulOutcome()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(250);

        // Act
        var outcome = Outcome.Successful("result data", duration);

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Output.Should().Be("result data");
        outcome.Duration.Should().Be(duration);
        outcome.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failed_CreatesFailedOutcome()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(3);
        var errors = new[] { "timeout", "connection reset" };

        // Act
        var outcome = Outcome.Failed("partial output", duration, errors);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Output.Should().Be("partial output");
        outcome.Duration.Should().Be(duration);
        outcome.Errors.Should().HaveCount(2);
        outcome.Errors.Should().Contain("timeout");
        outcome.Errors.Should().Contain("connection reset");
    }

    [Fact]
    public void Failed_WithEmptyErrors_CreatesFailedOutcomeWithNoErrors()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(1);

        // Act
        var outcome = Outcome.Failed("output", duration, Enumerable.Empty<string>());

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Successful_WithEmptyOutput_CreatesValidOutcome()
    {
        // Act
        var outcome = Outcome.Successful(string.Empty, TimeSpan.Zero);

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Output.Should().BeEmpty();
        outcome.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var errors = ImmutableList<string>.Empty;
        var duration = TimeSpan.FromSeconds(1);

        var outcome1 = new Outcome(true, "output", duration, errors);
        var outcome2 = new Outcome(true, "output", duration, errors);

        // Assert
        outcome1.Should().Be(outcome2);
    }

    [Fact]
    public void RecordEquality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var errors = ImmutableList<string>.Empty;
        var duration = TimeSpan.FromSeconds(1);

        var outcome1 = new Outcome(true, "output1", duration, errors);
        var outcome2 = new Outcome(true, "output2", duration, errors);

        // Assert
        outcome1.Should().NotBe(outcome2);
    }

    [Fact]
    public void Errors_IsImmutable()
    {
        // Arrange
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(1));

        // Act & Assert
        outcome.Errors.Should().BeOfType<ImmutableList<string>>();
    }

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = Outcome.Successful("original", TimeSpan.FromSeconds(1));

        // Act
        var modified = original with { Output = "modified" };

        // Assert
        modified.Output.Should().Be("modified");
        modified.Success.Should().BeTrue();
        original.Output.Should().Be("original");
    }
}
