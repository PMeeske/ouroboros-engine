// <copyright file="GoalTypeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class GoalTypeTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Enum.GetValues<GoalType>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(GoalType.Primary, 0)]
    [InlineData(GoalType.Secondary, 1)]
    [InlineData(GoalType.Instrumental, 2)]
    [InlineData(GoalType.Safety, 3)]
    public void Enum_HasExpectedIntegerValues(GoalType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData("Primary", true)]
    [InlineData("Secondary", true)]
    [InlineData("Instrumental", true)]
    [InlineData("Safety", true)]
    [InlineData("Unknown", false)]
    public void TryParse_VariousNames(string name, bool expected)
    {
        Enum.TryParse<GoalType>(name, out _).Should().Be(expected);
    }
}
