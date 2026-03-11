// <copyright file="ReasoningResultTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class ReasoningResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var steps = new List<ReasoningStep>
        {
            new(1, "step one", "rule1", ReasoningStepType.SymbolicDeduction)
        };
        var duration = TimeSpan.FromMilliseconds(250);

        // Act
        var result = new ReasoningResult(
            "What is 2+2?", "4", ReasoningMode.SymbolicFirst,
            steps, 0.95, true, false, duration);

        // Assert
        result.Query.Should().Be("What is 2+2?");
        result.Answer.Should().Be("4");
        result.ModeUsed.Should().Be(ReasoningMode.SymbolicFirst);
        result.Steps.Should().HaveCount(1);
        result.Confidence.Should().Be(0.95);
        result.SymbolicSucceeded.Should().BeTrue();
        result.NeuralSucceeded.Should().BeFalse();
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var steps = new List<ReasoningStep>();
        var duration = TimeSpan.FromSeconds(1);
        var a = new ReasoningResult("q", "a", ReasoningMode.Parallel, steps, 0.5, true, true, duration);
        var b = new ReasoningResult("q", "a", ReasoningMode.Parallel, steps, 0.5, true, true, duration);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var result = new ReasoningResult(
            "q", "a", ReasoningMode.NeuralOnly, new List<ReasoningStep>(),
            0.5, false, true, TimeSpan.Zero);

        // Act
        var modified = result with { Confidence = 0.99, Answer = "updated" };

        // Assert
        modified.Confidence.Should().Be(0.99);
        modified.Answer.Should().Be("updated");
        modified.Query.Should().Be("q");
    }
}
