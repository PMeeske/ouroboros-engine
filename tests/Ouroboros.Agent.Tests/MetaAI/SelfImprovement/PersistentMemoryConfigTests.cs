// <copyright file="PersistentMemoryConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class PersistentMemoryConfigTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var config = new PersistentMemoryConfig();

        config.ShortTermCapacity.Should().Be(100);
        config.LongTermCapacity.Should().Be(1000);
        config.ConsolidationThreshold.Should().Be(0.7);
        config.ConsolidationInterval.Should().Be(default(TimeSpan));
        config.EnableForgetting.Should().BeTrue();
        config.ForgettingThreshold.Should().Be(0.3);
    }

    [Fact]
    public void Constructor_WithCustomValues_OverridesDefaults()
    {
        var config = new PersistentMemoryConfig(
            ShortTermCapacity: 50,
            LongTermCapacity: 500,
            ConsolidationThreshold: 0.5,
            ConsolidationInterval: TimeSpan.FromMinutes(10),
            EnableForgetting: false,
            ForgettingThreshold: 0.1);

        config.ShortTermCapacity.Should().Be(50);
        config.LongTermCapacity.Should().Be(500);
        config.ConsolidationThreshold.Should().Be(0.5);
        config.ConsolidationInterval.Should().Be(TimeSpan.FromMinutes(10));
        config.EnableForgetting.Should().BeFalse();
        config.ForgettingThreshold.Should().Be(0.1);
    }

    [Fact]
    public void With_CanModifySingleProperty()
    {
        var config = new PersistentMemoryConfig();

        var modified = config with { ShortTermCapacity = 200 };

        modified.ShortTermCapacity.Should().Be(200);
        modified.LongTermCapacity.Should().Be(config.LongTermCapacity);
    }

    [Fact]
    public void Equality_SameDefaults_AreEqual()
    {
        var a = new PersistentMemoryConfig();
        var b = new PersistentMemoryConfig();

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new PersistentMemoryConfig();
        var b = new PersistentMemoryConfig(ShortTermCapacity: 999);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_DisableForgetting_SetsCorrectly()
    {
        var config = new PersistentMemoryConfig() with { EnableForgetting = false };

        config.EnableForgetting.Should().BeFalse();
    }
}
