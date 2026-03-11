// <copyright file="AdditionalQueryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public sealed class AdditionalQueryTests
{
    [Fact]
    public void ClassifyUseCaseQuery_SetsPrompt()
    {
        // Act
        var query = new ClassifyUseCaseQuery("What is the weather?");

        // Assert
        query.Prompt.Should().Be("What is the weather?");
    }

    [Fact]
    public void GetOrchestratorMetricsQuery_SetsName()
    {
        // Act
        var query = new GetOrchestratorMetricsQuery("SmartModel");

        // Assert
        query.OrchestratorName.Should().Be("SmartModel");
    }

    [Fact]
    public void ValidateReadinessQuery_SetsName()
    {
        // Act
        var query = new ValidateReadinessQuery("ConsolidatedMind");

        // Assert
        query.OrchestratorName.Should().Be("ConsolidatedMind");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new ClassifyUseCaseQuery("test");
        var b = new ClassifyUseCaseQuery("test");

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentValues()
    {
        // Arrange
        var a = new GetOrchestratorMetricsQuery("A");
        var b = new GetOrchestratorMetricsQuery("B");

        // Assert
        a.Should().NotBe(b);
    }
}
