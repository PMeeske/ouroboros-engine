// <copyright file="MeTTaRulePersistenceUnitTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.MeTTa.Persistence;
using Xunit;

namespace Ouroboros.MeTTa.Tests.Persistence;

public sealed class MeTTaRulePersistenceUnitTests
{
    [Fact]
    public void ComputeFingerprintVector_IsDeterministic()
    {
        const string text = "(implies (foo $x) (bar $x))";
        var a = MeTTaRulePersistence.ComputeFingerprintVector(text);
        var b = MeTTaRulePersistence.ComputeFingerprintVector(text);

        a.Should().Equal(b);
        a.Length.Should().Be(32);
    }

    [Fact]
    public void ComputeFingerprintVector_VariesWithInput()
    {
        var a = MeTTaRulePersistence.ComputeFingerprintVector("(rule a)");
        var b = MeTTaRulePersistence.ComputeFingerprintVector("(rule b)");

        a.Should().NotEqual(b);
    }

    [Fact]
    public void ComputeFingerprintVector_StaysInUnitBox()
    {
        var v = MeTTaRulePersistence.ComputeFingerprintVector("(any text here)");

        foreach (var f in v)
        {
            f.Should().BeInRange(-1.0f, 1.0f);
        }
    }

    [Fact]
    public void ComputePointUuid_IsStableForSameRule()
    {
        var rule = new MeTTaRule(
            AtomText: "(implies x y)",
            SessionId: "session-1",
            Step: 7,
            QualityScore: 0.92,
            Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));

        string a = MeTTaRulePersistence.ComputePointUuid(rule);
        string b = MeTTaRulePersistence.ComputePointUuid(rule);

        a.Should().Be(b);
        Guid.TryParse(a, out _).Should().BeTrue();
    }

    [Fact]
    public void ComputePointUuid_DiffersForDifferentSteps()
    {
        var ruleA = new MeTTaRule("text", "s", 1, 0.5, DateTimeOffset.UnixEpoch);
        var ruleB = ruleA with { Step = 2 };

        MeTTaRulePersistence.ComputePointUuid(ruleA)
            .Should().NotBe(MeTTaRulePersistence.ComputePointUuid(ruleB));
    }
}
