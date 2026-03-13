// <copyright file="TaskDetectionStrategyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers.Routing;

/// <summary>
/// Unit tests for <see cref="TaskDetectionStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskDetectionStrategyTests
{
    [Fact]
    public void Enum_HasThreeMembers()
    {
        // Arrange & Act
        var values = Enum.GetValues<TaskDetectionStrategy>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(TaskDetectionStrategy.Heuristic, 0)]
    [InlineData(TaskDetectionStrategy.RuleBased, 1)]
    [InlineData(TaskDetectionStrategy.Hybrid, 2)]
    public void Enum_HasExpectedValues(TaskDetectionStrategy strategy, int expected)
    {
        // Act
        var intValue = (int)strategy;

        // Assert
        intValue.Should().Be(expected);
    }

    [Fact]
    public void Enum_ContainsHeuristic()
    {
        // Assert
        Enum.IsDefined(TaskDetectionStrategy.Heuristic).Should().BeTrue();
    }

    [Fact]
    public void Enum_ContainsRuleBased()
    {
        // Assert
        Enum.IsDefined(TaskDetectionStrategy.RuleBased).Should().BeTrue();
    }

    [Fact]
    public void Enum_ContainsHybrid()
    {
        // Assert
        Enum.IsDefined(TaskDetectionStrategy.Hybrid).Should().BeTrue();
    }

    [Fact]
    public void Enum_ToString_ReturnsCorrectNames()
    {
        // Assert
        TaskDetectionStrategy.Heuristic.ToString().Should().Be("Heuristic");
        TaskDetectionStrategy.RuleBased.ToString().Should().Be("RuleBased");
        TaskDetectionStrategy.Hybrid.ToString().Should().Be("Hybrid");
    }

    [Fact]
    public void Enum_Parse_RoundTrips()
    {
        // Arrange & Act & Assert
        Enum.Parse<TaskDetectionStrategy>("Heuristic").Should().Be(TaskDetectionStrategy.Heuristic);
        Enum.Parse<TaskDetectionStrategy>("RuleBased").Should().Be(TaskDetectionStrategy.RuleBased);
        Enum.Parse<TaskDetectionStrategy>("Hybrid").Should().Be(TaskDetectionStrategy.Hybrid);
    }
}
