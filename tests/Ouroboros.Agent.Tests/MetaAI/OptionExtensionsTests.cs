using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Monads;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OptionExtensionsTests
{
    [Fact]
    public void ToOption_WithNonNullValue_ShouldReturnSome()
    {
        // Arrange
        string value = "hello";

        // Act
        var result = value.ToOption();

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void ToOption_WithNullValue_ShouldReturnNone()
    {
        // Arrange
        string? value = null;

        // Act
        var result = value.ToOption();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_WithReferenceType_ShouldWork()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };

        // Act
        var result = list.ToOption();

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void ToOption_WithNullReferenceType_ShouldReturnNone()
    {
        // Arrange
        List<int>? list = null;

        // Act
        var result = list.ToOption();

        // Assert
        result.HasValue.Should().BeFalse();
    }
}
