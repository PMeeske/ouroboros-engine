namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class GradientLearnerConfigTests
{
    [Fact]
    public void Default_HasValidSettings()
    {
        var config = GradientLearnerConfig.Default;
        config.Should().NotBeNull();
        config.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Conservative_HasValidSettings()
    {
        var config = GradientLearnerConfig.Conservative;
        config.Should().NotBeNull();
        config.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Aggressive_HasValidSettings()
    {
        var config = GradientLearnerConfig.Aggressive;
        config.Should().NotBeNull();
        config.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Configs_AreDifferent()
    {
        GradientLearnerConfig.Default.Should().NotBe(GradientLearnerConfig.Aggressive);
        GradientLearnerConfig.Default.Should().NotBe(GradientLearnerConfig.Conservative);
    }
}
