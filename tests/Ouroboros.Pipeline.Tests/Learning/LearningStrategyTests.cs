using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class LearningStrategyTests
{
    [Fact]
    public void Default_ReturnsStrategyWithExpectedDefaults()
    {
        // Act
        var strategy = LearningStrategy.Default;

        // Assert
        strategy.Name.Should().Be("Default");
        strategy.LearningRate.Should().Be(0.001);
        strategy.ExplorationRate.Should().Be(0.1);
        strategy.DiscountFactor.Should().Be(0.99);
        strategy.BatchSize.Should().Be(32);
        strategy.Parameters.Should().BeEmpty();
        strategy.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Exploratory_ReturnsExplorationFocusedStrategy()
    {
        // Act
        var strategy = LearningStrategy.Exploratory();

        // Assert
        strategy.Name.Should().Be("Exploratory");
        strategy.LearningRate.Should().Be(0.01);
        strategy.ExplorationRate.Should().Be(0.5);
        strategy.DiscountFactor.Should().Be(0.95);
        strategy.BatchSize.Should().Be(64);
        strategy.Parameters.Should().ContainKey("temperature");
        strategy.Parameters["temperature"].Should().Be(1.5);
        strategy.Parameters.Should().ContainKey("curiosity_weight");
        strategy.Parameters["curiosity_weight"].Should().Be(0.3);
    }

    [Fact]
    public void Exploratory_WithCustomName_UsesProvidedName()
    {
        // Act
        var strategy = LearningStrategy.Exploratory("CustomExplorer");

        // Assert
        strategy.Name.Should().Be("CustomExplorer");
    }

    [Fact]
    public void Exploitative_ReturnsExploitationFocusedStrategy()
    {
        // Act
        var strategy = LearningStrategy.Exploitative();

        // Assert
        strategy.Name.Should().Be("Exploitative");
        strategy.LearningRate.Should().Be(0.0001);
        strategy.ExplorationRate.Should().Be(0.01);
        strategy.DiscountFactor.Should().Be(0.999);
        strategy.BatchSize.Should().Be(128);
        strategy.Parameters["temperature"].Should().Be(0.5);
        strategy.Parameters["curiosity_weight"].Should().Be(0.05);
    }

    [Fact]
    public void WithLearningRate_ClampsToValidRange()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act & Assert
        strategy.WithLearningRate(0.5).LearningRate.Should().Be(0.5);
        strategy.WithLearningRate(2.0).LearningRate.Should().Be(1.0);
        strategy.WithLearningRate(-1.0).LearningRate.Should().Be(1e-7);
    }

    [Fact]
    public void WithExplorationRate_ClampsToValidRange()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act & Assert
        strategy.WithExplorationRate(0.5).ExplorationRate.Should().Be(0.5);
        strategy.WithExplorationRate(2.0).ExplorationRate.Should().Be(1.0);
        strategy.WithExplorationRate(-0.5).ExplorationRate.Should().Be(0.0);
    }

    [Fact]
    public void WithDiscountFactor_ClampsToValidRange()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act & Assert
        strategy.WithDiscountFactor(0.5).DiscountFactor.Should().Be(0.5);
        strategy.WithDiscountFactor(1.5).DiscountFactor.Should().Be(1.0);
        strategy.WithDiscountFactor(-0.1).DiscountFactor.Should().Be(0.0);
    }

    [Fact]
    public void WithParameter_AddsOrUpdatesParameter()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act
        var updated = strategy.WithParameter("temperature", 0.8);

        // Assert
        updated.Parameters.Should().ContainKey("temperature");
        updated.Parameters["temperature"].Should().Be(0.8);
    }

    [Fact]
    public void WithParameter_UpdatesExistingParameter()
    {
        // Arrange
        var strategy = LearningStrategy.Exploratory();

        // Act
        var updated = strategy.WithParameter("temperature", 2.0);

        // Assert
        updated.Parameters["temperature"].Should().Be(2.0);
    }

    [Fact]
    public void Validate_WithValidStrategy_ReturnsSuccess()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { Name = "" };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void Validate_WithInvalidLearningRate_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { LearningRate = 0 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Learning rate");
    }

    [Fact]
    public void Validate_WithNegativeExplorationRate_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { ExplorationRate = -0.1 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Exploration rate");
    }

    [Fact]
    public void Validate_WithNegativeDiscountFactor_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { DiscountFactor = -0.1 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Discount factor");
    }

    [Fact]
    public void Validate_WithZeroBatchSize_ReturnsFailure()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { BatchSize = 0 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Batch size");
    }

    [Fact]
    public void WithLearningRate_ReturnsNewInstance()
    {
        // Arrange
        var original = LearningStrategy.Default;

        // Act
        var updated = original.WithLearningRate(0.5);

        // Assert
        updated.Should().NotBeSameAs(original);
        original.LearningRate.Should().Be(0.001);
    }

    [Fact]
    public void Default_GeneratesUniqueIdEachTime()
    {
        // Act
        var strategy1 = LearningStrategy.Default;
        var strategy2 = LearningStrategy.Default;

        // Assert
        strategy1.Id.Should().NotBe(strategy2.Id);
    }
}
