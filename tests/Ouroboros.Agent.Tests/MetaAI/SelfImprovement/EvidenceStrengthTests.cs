// <copyright file="EvidenceStrengthTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class EvidenceStrengthTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Enum.GetValues<EvidenceStrength>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(EvidenceStrength.Negligible, 0)]
    [InlineData(EvidenceStrength.Substantial, 1)]
    [InlineData(EvidenceStrength.Strong, 2)]
    [InlineData(EvidenceStrength.VeryStrong, 3)]
    [InlineData(EvidenceStrength.Decisive, 4)]
    public void Enum_HasExpectedIntegerValues(EvidenceStrength strength, int expected)
    {
        ((int)strength).Should().Be(expected);
    }

    [Fact]
    public void Negligible_IsLowestStrength()
    {
        EvidenceStrength.Negligible.Should().BeLessThan(EvidenceStrength.Substantial);
    }

    [Fact]
    public void Decisive_IsHighestStrength()
    {
        EvidenceStrength.Decisive.Should().BeGreaterThan(EvidenceStrength.VeryStrong);
    }

    [Theory]
    [InlineData("Negligible", true)]
    [InlineData("Substantial", true)]
    [InlineData("Strong", true)]
    [InlineData("VeryStrong", true)]
    [InlineData("Decisive", true)]
    [InlineData("Invalid", false)]
    public void TryParse_VariousNames(string name, bool expected)
    {
        Enum.TryParse<EvidenceStrength>(name, out _).Should().Be(expected);
    }
}
