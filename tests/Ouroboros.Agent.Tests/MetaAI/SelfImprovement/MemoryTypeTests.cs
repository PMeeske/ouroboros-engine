// <copyright file="MemoryTypeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class MemoryTypeTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Enum.GetValues<MemoryType>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(MemoryType.Episodic, 0)]
    [InlineData(MemoryType.Semantic, 1)]
    public void Enum_HasExpectedIntegerValues(MemoryType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Fact]
    public void Episodic_IsDifferentFromSemantic()
    {
        MemoryType.Episodic.Should().NotBe(MemoryType.Semantic);
    }

    [Theory]
    [InlineData("Episodic", true)]
    [InlineData("Semantic", true)]
    [InlineData("Procedural", false)]
    public void TryParse_VariousNames(string name, bool expected)
    {
        Enum.TryParse<MemoryType>(name, out _).Should().Be(expected);
    }
}
