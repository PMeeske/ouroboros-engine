// <copyright file="RuleSourceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public sealed class RuleSourceTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<RuleSource>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(RuleSource.ExtractedFromSkill, 0)]
    [InlineData(RuleSource.LearnedFromExperience, 1)]
    [InlineData(RuleSource.UserProvided, 2)]
    [InlineData(RuleSource.InferredFromHypothesis, 3)]
    public void Enum_OrdinalStability(RuleSource source, int expected)
    {
        ((int)source).Should().Be(expected);
    }

    [Theory]
    [InlineData(RuleSource.ExtractedFromSkill, "ExtractedFromSkill")]
    [InlineData(RuleSource.LearnedFromExperience, "LearnedFromExperience")]
    [InlineData(RuleSource.UserProvided, "UserProvided")]
    [InlineData(RuleSource.InferredFromHypothesis, "InferredFromHypothesis")]
    public void Enum_ToStringReturnsName(RuleSource source, string expected)
    {
        source.ToString().Should().Be(expected);
    }
}
