// <copyright file="ExpressionTypeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class ExpressionTypeTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<ExpressionType>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(ExpressionType.Atom, 0)]
    [InlineData(ExpressionType.Variable, 1)]
    [InlineData(ExpressionType.Expression, 2)]
    [InlineData(ExpressionType.Function, 3)]
    [InlineData(ExpressionType.Rule, 4)]
    [InlineData(ExpressionType.Query, 5)]
    public void Enum_OrdinalStability(ExpressionType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData(ExpressionType.Atom, "Atom")]
    [InlineData(ExpressionType.Variable, "Variable")]
    [InlineData(ExpressionType.Expression, "Expression")]
    [InlineData(ExpressionType.Function, "Function")]
    [InlineData(ExpressionType.Rule, "Rule")]
    [InlineData(ExpressionType.Query, "Query")]
    public void Enum_ToStringReturnsName(ExpressionType type, string expected)
    {
        type.ToString().Should().Be(expected);
    }
}
