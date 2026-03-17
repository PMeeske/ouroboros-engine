// <copyright file="CommitmentStatusTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class CommitmentStatusTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Enum.GetValues<CommitmentStatus>().Should().HaveCount(6);
    }

    [Theory]
    [InlineData(CommitmentStatus.Planned, 0)]
    [InlineData(CommitmentStatus.InProgress, 1)]
    [InlineData(CommitmentStatus.Completed, 2)]
    [InlineData(CommitmentStatus.Failed, 3)]
    [InlineData(CommitmentStatus.Cancelled, 4)]
    [InlineData(CommitmentStatus.AtRisk, 5)]
    public void Enum_HasExpectedIntegerValues(CommitmentStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Theory]
    [InlineData("Planned", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Failed", true)]
    [InlineData("Cancelled", true)]
    [InlineData("AtRisk", true)]
    [InlineData("Unknown", false)]
    public void TryParse_VariousNames(string name, bool expected)
    {
        Enum.TryParse<CommitmentStatus>(name, out _).Should().Be(expected);
    }

    [Fact]
    public void Planned_IsDistinctFromOtherStatuses()
    {
        CommitmentStatus.Planned.Should().NotBe(CommitmentStatus.InProgress);
        CommitmentStatus.Planned.Should().NotBe(CommitmentStatus.Completed);
        CommitmentStatus.Planned.Should().NotBe(CommitmentStatus.Failed);
    }
}
