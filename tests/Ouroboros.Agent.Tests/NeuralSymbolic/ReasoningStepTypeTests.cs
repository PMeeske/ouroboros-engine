// <copyright file="ReasoningStepTypeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class ReasoningStepTypeTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<ReasoningStepType>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(ReasoningStepType.SymbolicDeduction, 0)]
    [InlineData(ReasoningStepType.SymbolicInduction, 1)]
    [InlineData(ReasoningStepType.NeuralInference, 2)]
    [InlineData(ReasoningStepType.Combination, 3)]
    public void Enum_OrdinalStability(ReasoningStepType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData(ReasoningStepType.SymbolicDeduction, "SymbolicDeduction")]
    [InlineData(ReasoningStepType.SymbolicInduction, "SymbolicInduction")]
    [InlineData(ReasoningStepType.NeuralInference, "NeuralInference")]
    [InlineData(ReasoningStepType.Combination, "Combination")]
    public void Enum_ToStringReturnsName(ReasoningStepType type, string expected)
    {
        type.ToString().Should().Be(expected);
    }
}
