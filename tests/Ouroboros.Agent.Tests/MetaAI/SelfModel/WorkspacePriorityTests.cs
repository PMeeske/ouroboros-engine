// <copyright file="WorkspacePriorityTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspacePriorityTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Enum.GetValues<WorkspacePriority>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(WorkspacePriority.Low, 0)]
    [InlineData(WorkspacePriority.Normal, 1)]
    [InlineData(WorkspacePriority.High, 2)]
    [InlineData(WorkspacePriority.Critical, 3)]
    public void Enum_HasExpectedIntegerValues(WorkspacePriority priority, int expected)
    {
        ((int)priority).Should().Be(expected);
    }

    [Fact]
    public void Low_IsLowestPriority()
    {
        WorkspacePriority.Low.Should().BeLessThan(WorkspacePriority.Normal);
    }

    [Fact]
    public void Critical_IsHighestPriority()
    {
        WorkspacePriority.Critical.Should().BeGreaterThan(WorkspacePriority.High);
    }

    [Theory]
    [InlineData("Low", true)]
    [InlineData("Normal", true)]
    [InlineData("High", true)]
    [InlineData("Critical", true)]
    [InlineData("Medium", false)]
    public void TryParse_VariousNames(string name, bool expected)
    {
        Enum.TryParse<WorkspacePriority>(name, out _).Should().Be(expected);
    }

    [Fact]
    public void CastToInt_CanBeUsedForArithmetic()
    {
        // The int values allow priority math (used in GetAttentionWeight)
        var priority = WorkspacePriority.Critical;
        double normalized = (int)priority / 3.0;

        normalized.Should().BeApproximately(1.0, 0.001);
    }
}
