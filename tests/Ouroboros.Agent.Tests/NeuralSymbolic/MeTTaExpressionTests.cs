// <copyright file="MeTTaExpressionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public class MeTTaExpressionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var expr = new MeTTaExpression(
            "(= (add $x $y) (+ $x $y))",
            ExpressionType.Expression,
            new List<string> { "add", "+" },
            new List<string> { "$x", "$y" },
            new Dictionary<string, object> { ["priority"] = 1 });

        expr.RawExpression.Should().Be("(= (add $x $y) (+ $x $y))");
        expr.Type.Should().Be(ExpressionType.Expression);
        expr.Symbols.Should().HaveCount(2);
        expr.Variables.Should().HaveCount(2);
        expr.Metadata.Should().ContainKey("priority");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new MeTTaExpression(
            "(fact x)",
            ExpressionType.Atom,
            new List<string> { "x" },
            new List<string>(),
            new Dictionary<string, object>());

        var b = new MeTTaExpression(
            "(fact x)",
            ExpressionType.Atom,
            new List<string> { "x" },
            new List<string>(),
            new Dictionary<string, object>());

        a.Should().BeEquivalentTo(b);
    }
}
