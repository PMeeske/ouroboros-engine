// <copyright file="SpecializedRoleTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class SpecializedRoleTests
{
    [Fact]
    public void Enum_HasExpectedCount()
    {
        Enum.GetValues<SpecializedRole>().Should().HaveCount(11);
    }

    [Theory]
    [InlineData(SpecializedRole.QuickResponse, 0)]
    [InlineData(SpecializedRole.DeepReasoning, 1)]
    [InlineData(SpecializedRole.CodeExpert, 2)]
    [InlineData(SpecializedRole.Creative, 3)]
    [InlineData(SpecializedRole.Mathematical, 4)]
    [InlineData(SpecializedRole.Analyst, 5)]
    [InlineData(SpecializedRole.Synthesizer, 6)]
    [InlineData(SpecializedRole.Planner, 7)]
    [InlineData(SpecializedRole.Verifier, 8)]
    [InlineData(SpecializedRole.MetaCognitive, 9)]
    [InlineData(SpecializedRole.SymbolicReasoner, 10)]
    public void Enum_OrdinalStability(SpecializedRole role, int expected)
    {
        ((int)role).Should().Be(expected);
    }

    [Theory]
    [InlineData(SpecializedRole.QuickResponse, "QuickResponse")]
    [InlineData(SpecializedRole.DeepReasoning, "DeepReasoning")]
    [InlineData(SpecializedRole.CodeExpert, "CodeExpert")]
    [InlineData(SpecializedRole.Creative, "Creative")]
    [InlineData(SpecializedRole.Mathematical, "Mathematical")]
    [InlineData(SpecializedRole.Analyst, "Analyst")]
    [InlineData(SpecializedRole.Synthesizer, "Synthesizer")]
    [InlineData(SpecializedRole.Planner, "Planner")]
    [InlineData(SpecializedRole.Verifier, "Verifier")]
    [InlineData(SpecializedRole.MetaCognitive, "MetaCognitive")]
    [InlineData(SpecializedRole.SymbolicReasoner, "SymbolicReasoner")]
    public void Enum_ToStringReturnsName(SpecializedRole role, string expected)
    {
        role.ToString().Should().Be(expected);
    }
}
