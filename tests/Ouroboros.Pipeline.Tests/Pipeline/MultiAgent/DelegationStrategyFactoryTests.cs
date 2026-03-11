namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class DelegationStrategyFactoryTests
{
    [Fact]
    public void ByCapability_ReturnsCapabilityBasedStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.ByCapability();

        // Assert
        strategy.Should().BeOfType<CapabilityBasedStrategy>();
        strategy.Name.Should().Be("CapabilityBased");
    }

    [Fact]
    public void ByRole_ReturnsRoleBasedStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.ByRole();

        // Assert
        strategy.Should().BeOfType<RoleBasedStrategy>();
        strategy.Name.Should().Be("RoleBased");
    }

    [Fact]
    public void ByLoad_ReturnsLoadBalancingStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.ByLoad();

        // Assert
        strategy.Should().BeOfType<LoadBalancingStrategy>();
        strategy.Name.Should().Be("LoadBalancing");
    }

    [Fact]
    public void RoundRobin_ReturnsRoundRobinStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.RoundRobin();

        // Assert
        strategy.Should().BeOfType<RoundRobinStrategy>();
        strategy.Name.Should().Be("RoundRobin");
    }

    [Fact]
    public void BestFit_ReturnsBestFitStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.BestFit();

        // Assert
        strategy.Should().BeOfType<BestFitStrategy>();
        strategy.Name.Should().Be("BestFit");
    }

    [Fact]
    public void Composite_ReturnsCompositeStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.Composite(
            (new RoundRobinStrategy(), 1.0));

        // Assert
        strategy.Name.Should().Be("Composite");
    }

    [Fact]
    public void Composite_WithNullStrategies_Throws()
    {
        // Act
        Action act = () => DelegationStrategyFactory.Composite(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Composite_WithEmptyStrategies_Throws()
    {
        // Act
        Action act = () => DelegationStrategyFactory.Composite();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Balanced_ReturnsCompositeStrategy()
    {
        // Act
        var strategy = DelegationStrategyFactory.Balanced();

        // Assert
        strategy.Name.Should().Be("Composite");
    }
}
