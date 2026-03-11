// <copyright file="NanoAtomPhaseTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Tests.NanoAtoms;

[Trait("Category", "Unit")]
public sealed class NanoAtomPhaseTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<NanoAtomPhase>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(NanoAtomPhase.Idle, 0)]
    [InlineData(NanoAtomPhase.Receive, 1)]
    [InlineData(NanoAtomPhase.Process, 2)]
    [InlineData(NanoAtomPhase.Digest, 3)]
    [InlineData(NanoAtomPhase.Emit, 4)]
    public void Enum_OrdinalStability(NanoAtomPhase phase, int expected)
    {
        ((int)phase).Should().Be(expected);
    }

    [Theory]
    [InlineData(NanoAtomPhase.Idle, "Idle")]
    [InlineData(NanoAtomPhase.Receive, "Receive")]
    [InlineData(NanoAtomPhase.Process, "Process")]
    [InlineData(NanoAtomPhase.Digest, "Digest")]
    [InlineData(NanoAtomPhase.Emit, "Emit")]
    public void Enum_ToStringReturnsName(NanoAtomPhase phase, string expected)
    {
        phase.ToString().Should().Be(expected);
    }
}
