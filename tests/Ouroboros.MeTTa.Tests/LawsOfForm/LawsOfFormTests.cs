// <copyright file="LawsOfFormTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.MeTTa.LawsOfForm;
using Xunit;

namespace Ouroboros.MeTTa.Tests.LawsOfForm;

public sealed class LawsOfFormTests
{
    [Fact]
    public void Cross_TogglesVoidAndMark()
    {
        DistinctionTracker tracker = new();
        CrossOperation cross = new(tracker);

        cross.Execute("x").Should().Be(DistinctionState.Mark);
        cross.Execute("x").Should().Be(DistinctionState.Void);
        cross.Execute("x").Should().Be(DistinctionState.Mark);
    }

    [Fact]
    public void Reentry_ForcesImaginary()
    {
        DistinctionTracker tracker = new();
        ReentryOperation reentry = new(tracker);

        reentry.Execute("x").Should().Be(DistinctionState.Imaginary);
        tracker.Get("x").Should().Be(DistinctionState.Imaginary);
    }

    [Fact]
    public void Call_DoesNotMutate()
    {
        DistinctionTracker tracker = new();
        tracker.Set("y", DistinctionState.Mark);
        CallOperation call = new(tracker);

        call.Execute("y").Should().Be(DistinctionState.Mark);
        call.Execute("y").Should().Be(DistinctionState.Mark);
        tracker.Get("y").Should().Be(DistinctionState.Mark);
    }

    [Fact]
    public void ThreeValuedLogic_FullTableHasNineRows()
    {
        var rows = ThreeValuedLogic.FullTable();
        rows.Count.Should().Be(9);
    }

    [Theory]
    [InlineData(DistinctionState.Mark, DistinctionState.Mark, DistinctionState.Mark)]
    [InlineData(DistinctionState.Void, DistinctionState.Mark, DistinctionState.Void)]
    [InlineData(DistinctionState.Imaginary, DistinctionState.Mark, DistinctionState.Imaginary)]
    [InlineData(DistinctionState.Imaginary, DistinctionState.Void, DistinctionState.Void)]
    public void ThreeValuedLogic_AndIsKleeneCorrect(DistinctionState a, DistinctionState b, DistinctionState expected)
    {
        ThreeValuedLogic.And(a, b).Should().Be(expected);
    }

    [Fact]
    public void LawsOfFormRegistration_RegistersAllThreeOps()
    {
        GroundedRegistry registry = new();
        DistinctionTracker tracker = new();

        LawsOfFormRegistration.RegisterAll(registry, tracker);

        registry.Contains("cross").Should().BeTrue();
        registry.Contains("call").Should().BeTrue();
        registry.Contains("reentry").Should().BeTrue();
    }
}
