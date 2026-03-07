// <copyright file="MindConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class MindConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var config = new MindConfig();

        config.EnableThinking.Should().BeTrue();
        config.EnableVerification.Should().BeTrue();
        config.EnableParallelExecution.Should().BeTrue();
        config.MaxParallelism.Should().Be(3);
        config.DefaultTimeout.Should().BeNull();
        config.FallbackOnError.Should().BeTrue();
    }

    [Fact]
    public void Minimal_DisablesAdvancedFeatures()
    {
        var config = MindConfig.Minimal();

        config.EnableThinking.Should().BeFalse();
        config.EnableVerification.Should().BeFalse();
        config.EnableParallelExecution.Should().BeFalse();
        config.MaxParallelism.Should().Be(1);
        config.FallbackOnError.Should().BeTrue();
    }

    [Fact]
    public void HighQuality_EnablesEverything()
    {
        var config = MindConfig.HighQuality();

        config.EnableThinking.Should().BeTrue();
        config.EnableVerification.Should().BeTrue();
        config.EnableParallelExecution.Should().BeTrue();
        config.MaxParallelism.Should().Be(4);
        config.DefaultTimeout.Should().Be(TimeSpan.FromMinutes(5));
        config.FallbackOnError.Should().BeTrue();
    }

    [Fact]
    public void With_Expression_OverridesField()
    {
        var config = new MindConfig() with { MaxParallelism = 10 };
        config.MaxParallelism.Should().Be(10);
    }

    [Fact]
    public void Record_Equality_Works()
    {
        var a = MindConfig.Minimal();
        var b = MindConfig.Minimal();

        a.Should().Be(b);
    }

    [Fact]
    public void Record_Inequality_DifferentValues()
    {
        var minimal = MindConfig.Minimal();
        var highQuality = MindConfig.HighQuality();

        minimal.Should().NotBe(highQuality);
    }
}
