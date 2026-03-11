// <copyright file="TaskStatusTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using AffectTaskStatus = Ouroboros.Agent.MetaAI.Affect.TaskStatus;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public sealed class TaskStatusTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<AffectTaskStatus>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(AffectTaskStatus.Pending, 0)]
    [InlineData(AffectTaskStatus.InProgress, 1)]
    [InlineData(AffectTaskStatus.Completed, 2)]
    [InlineData(AffectTaskStatus.Failed, 3)]
    [InlineData(AffectTaskStatus.Cancelled, 4)]
    [InlineData(AffectTaskStatus.Blocked, 5)]
    public void Enum_OrdinalStability(AffectTaskStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Theory]
    [InlineData(AffectTaskStatus.Pending, "Pending")]
    [InlineData(AffectTaskStatus.InProgress, "InProgress")]
    [InlineData(AffectTaskStatus.Completed, "Completed")]
    [InlineData(AffectTaskStatus.Failed, "Failed")]
    [InlineData(AffectTaskStatus.Cancelled, "Cancelled")]
    [InlineData(AffectTaskStatus.Blocked, "Blocked")]
    public void Enum_ToStringReturnsName(AffectTaskStatus status, string expected)
    {
        status.ToString().Should().Be(expected);
    }
}
