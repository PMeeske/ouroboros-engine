using Ouroboros.Providers;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class DecompositionConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var config = DecompositionConfig.Default;

        config.MaxSubGoals.Should().Be(10);
        config.ParallelizeIndependent.Should().BeTrue();
        config.PreferLocalForSimple.Should().BeTrue();
        config.PremiumForSynthesis.Should().BeTrue();
        config.UsePipelineGoalDecomposer.Should().BeFalse();
    }

    [Fact]
    public void LocalFirst_PrefersLocal()
    {
        var config = DecompositionConfig.LocalFirst;

        config.PreferLocalForSimple.Should().BeTrue();
        config.PremiumForSynthesis.Should().BeFalse();
    }

    [Fact]
    public void QualityFirst_PrefersCloud()
    {
        var config = DecompositionConfig.QualityFirst;

        config.PreferLocalForSimple.Should().BeFalse();
        config.PremiumForSynthesis.Should().BeTrue();
    }

    [Fact]
    public void PipelineIntegrated_UsesGoalDecomposer()
    {
        var config = DecompositionConfig.PipelineIntegrated;

        config.UsePipelineGoalDecomposer.Should().BeTrue();
    }

    [Fact]
    public void Default_TypeRouting_HasAllTypes()
    {
        var config = DecompositionConfig.Default;
        config.TypeRouting.Should().ContainKey(SubGoalType.Retrieval);
        config.TypeRouting.Should().ContainKey(SubGoalType.Coding);
        config.TypeRouting.Should().ContainKey(SubGoalType.Synthesis);
    }
}
