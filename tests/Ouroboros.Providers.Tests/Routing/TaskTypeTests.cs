// <copyright file="TaskTypeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers.Routing;

/// <summary>
/// Unit tests for <see cref="TaskType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskTypeTests
{
    [Fact]
    public void Enum_HasFiveMembers()
    {
        // Arrange & Act
        var values = Enum.GetValues<TaskType>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(TaskType.Simple, 0)]
    [InlineData(TaskType.Reasoning, 1)]
    [InlineData(TaskType.Planning, 2)]
    [InlineData(TaskType.Coding, 3)]
    [InlineData(TaskType.Unknown, 4)]
    public void Enum_HasExpectedValues(TaskType type, int expected)
    {
        // Act
        var intValue = (int)type;

        // Assert
        intValue.Should().Be(expected);
    }

    [Fact]
    public void Enum_ContainsSimple()
    {
        // Assert
        Enum.IsDefined(TaskType.Simple).Should().BeTrue();
    }

    [Fact]
    public void Enum_ContainsReasoning()
    {
        // Assert
        Enum.IsDefined(TaskType.Reasoning).Should().BeTrue();
    }

    [Fact]
    public void Enum_ContainsPlanning()
    {
        // Assert
        Enum.IsDefined(TaskType.Planning).Should().BeTrue();
    }

    [Fact]
    public void Enum_ContainsCoding()
    {
        // Assert
        Enum.IsDefined(TaskType.Coding).Should().BeTrue();
    }

    [Fact]
    public void Enum_ContainsUnknown()
    {
        // Assert
        Enum.IsDefined(TaskType.Unknown).Should().BeTrue();
    }

    [Fact]
    public void Enum_ToString_ReturnsCorrectNames()
    {
        // Assert
        TaskType.Simple.ToString().Should().Be("Simple");
        TaskType.Reasoning.ToString().Should().Be("Reasoning");
        TaskType.Planning.ToString().Should().Be("Planning");
        TaskType.Coding.ToString().Should().Be("Coding");
        TaskType.Unknown.ToString().Should().Be("Unknown");
    }

    [Fact]
    public void Enum_Parse_RoundTrips()
    {
        // Arrange & Act & Assert
        Enum.Parse<TaskType>("Simple").Should().Be(TaskType.Simple);
        Enum.Parse<TaskType>("Reasoning").Should().Be(TaskType.Reasoning);
        Enum.Parse<TaskType>("Planning").Should().Be(TaskType.Planning);
        Enum.Parse<TaskType>("Coding").Should().Be(TaskType.Coding);
        Enum.Parse<TaskType>("Unknown").Should().Be(TaskType.Unknown);
    }

    [Fact]
    public void Enum_Default_IsSimple()
    {
        // Arrange & Act
        TaskType defaultValue = default;

        // Assert
        defaultValue.Should().Be(TaskType.Simple);
    }
}
