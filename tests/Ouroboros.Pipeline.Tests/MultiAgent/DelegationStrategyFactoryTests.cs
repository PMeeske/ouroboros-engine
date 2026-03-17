using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class DelegationStrategyFactoryTests
{
    [Fact]
    public void ByCapability_ReturnsCapabilityBasedStrategy()
    {
        var strategy = DelegationStrategyFactory.ByCapability();
        strategy.Should().BeOfType<CapabilityBasedStrategy>();
        strategy.Name.Should().Be("CapabilityBased");
    }

    [Fact]
    public void ByRole_ReturnsRoleBasedStrategy()
    {
        var strategy = DelegationStrategyFactory.ByRole();
        strategy.Should().BeOfType<RoleBasedStrategy>();
        strategy.Name.Should().Be("RoleBased");
    }

    [Fact]
    public void ByLoad_ReturnsLoadBalancingStrategy()
    {
        var strategy = DelegationStrategyFactory.ByLoad();
        strategy.Should().BeOfType<LoadBalancingStrategy>();
        strategy.Name.Should().Be("LoadBalancing");
    }

    [Fact]
    public void RoundRobin_ReturnsRoundRobinStrategy()
    {
        var strategy = DelegationStrategyFactory.RoundRobin();
        strategy.Should().BeOfType<RoundRobinStrategy>();
        strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void BestFit_ReturnsBestFitStrategy()
    {
        var strategy = DelegationStrategyFactory.BestFit();
        strategy.Should().BeOfType<BestFitStrategy>();
        strategy.Name.Should().Be("BestFit");
    }

    [Fact]
    public void Composite_ReturnsCompositeStrategy()
    {
        var strategy = DelegationStrategyFactory.Composite(
            (new CapabilityBasedStrategy(), 0.5),
            (new LoadBalancingStrategy(), 0.5));
        strategy.Should().BeOfType<CompositeStrategy>();
        strategy.Name.Should().Be("Composite");
    }

    [Fact]
    public void Balanced_ReturnsCompositeStrategy()
    {
        var strategy = DelegationStrategyFactory.Balanced();
        strategy.Should().BeOfType<CompositeStrategy>();
        strategy.Name.Should().Be("Composite");
    }

    [Fact]
    public void Composite_WithNullStrategies_ThrowsArgumentNullException()
    {
        Action act = () => DelegationStrategyFactory.Composite(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Composite_WithEmptyStrategies_ThrowsArgumentException()
    {
        Action act = () => DelegationStrategyFactory.Composite();
        act.Should().Throw<ArgumentException>();
    }
}
