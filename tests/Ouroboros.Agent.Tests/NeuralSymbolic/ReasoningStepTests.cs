// <copyright file="ReasoningStepTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class ReasoningStepTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Act
        var step = new ReasoningStep(1, "Apply modus ponens", "rule_mp", ReasoningStepType.SymbolicDeduction);

        // Assert
        step.StepNumber.Should().Be(1);
        step.Description.Should().Be("Apply modus ponens");
        step.RuleApplied.Should().Be("rule_mp");
        step.Type.Should().Be(ReasoningStepType.SymbolicDeduction);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new ReasoningStep(2, "desc", "rule", ReasoningStepType.NeuralInference);
        var b = new ReasoningStep(2, "desc", "rule", ReasoningStepType.NeuralInference);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentType()
    {
        // Arrange
        var a = new ReasoningStep(1, "desc", "rule", ReasoningStepType.SymbolicDeduction);
        var b = new ReasoningStep(1, "desc", "rule", ReasoningStepType.NeuralInference);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var step = new ReasoningStep(1, "original", "rule1", ReasoningStepType.Combination);

        // Act
        var modified = step with { Description = "updated", StepNumber = 5 };

        // Assert
        modified.Description.Should().Be("updated");
        modified.StepNumber.Should().Be(5);
        modified.RuleApplied.Should().Be("rule1");
    }
}
