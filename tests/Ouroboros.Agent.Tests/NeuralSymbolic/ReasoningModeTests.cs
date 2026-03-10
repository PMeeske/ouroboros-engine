// <copyright file="ReasoningModeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class ReasoningModeTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<ReasoningMode>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(ReasoningMode.SymbolicFirst, 0)]
    [InlineData(ReasoningMode.NeuralFirst, 1)]
    [InlineData(ReasoningMode.Parallel, 2)]
    [InlineData(ReasoningMode.SymbolicOnly, 3)]
    [InlineData(ReasoningMode.NeuralOnly, 4)]
    public void Enum_OrdinalStability(ReasoningMode mode, int expected)
    {
        ((int)mode).Should().Be(expected);
    }

    [Theory]
    [InlineData(ReasoningMode.SymbolicFirst, "SymbolicFirst")]
    [InlineData(ReasoningMode.NeuralFirst, "NeuralFirst")]
    [InlineData(ReasoningMode.Parallel, "Parallel")]
    [InlineData(ReasoningMode.SymbolicOnly, "SymbolicOnly")]
    [InlineData(ReasoningMode.NeuralOnly, "NeuralOnly")]
    public void Enum_ToStringReturnsName(ReasoningMode mode, string expected)
    {
        mode.ToString().Should().Be(expected);
    }
}
