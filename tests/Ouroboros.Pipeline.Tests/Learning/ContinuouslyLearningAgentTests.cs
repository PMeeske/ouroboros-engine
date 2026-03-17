using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class ContinuouslyLearningAgentTests
{
    [Fact]
    public void Constructor_WithDefaults_InitializesCorrectly()
    {
        // Act
        var agent = new ContinuouslyLearningAgent();

        // Assert
        agent.AgentId.Should().NotBeEmpty();
        agent.GetPerformance().TotalInteractions.Should().Be(0);
        agent.GetAdaptationHistory().Should().BeEmpty();
        agent.GetExperienceCount().Should().Be(0);
    }

    [Fact]
    public void Constructor_WithCustomAgentId_UsesProvidedId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var agent = new ContinuouslyLearningAgent(agentId: id);

        // Assert
        agent.AgentId.Should().Be(id);
    }

    [Fact]
    public void RecordInteraction_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.RecordInteraction("Hello", "Hi there", 0.8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInteractions.Should().Be(1);
    }

    [Fact]
    public void RecordInteraction_WithEmptyInput_ReturnsFailure()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.RecordInteraction("", "output", 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Input");
    }

    [Fact]
    public void RecordInteraction_WithEmptyOutput_ReturnsFailure()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.RecordInteraction("input", "", 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Output");
    }

    [Fact]
    public void RecordInteraction_ClampsQualityToRange()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        agent.RecordInteraction("in", "out", 5.0);
        var perf = agent.GetPerformance();

        // Assert
        perf.AverageResponseQuality.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void RecordInteraction_AddsToExperienceBuffer()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        agent.RecordInteraction("in", "out", 0.5);

        // Assert
        agent.GetExperienceCount().Should().Be(1);
    }

    [Fact]
    public void RecordInteraction_UpdatesEMAPerformance()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        agent.RecordInteraction("in", "out", 0.8);
        var perf = agent.GetPerformance();

        // Assert
        perf.AverageResponseQuality.Should().Be(0.8); // First value initializes EMA
    }

    [Fact]
    public void RecordInteraction_MultipleInteractions_UpdatesEMA()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(EmaAlpha: 0.5));

        // Act
        agent.RecordInteraction("in1", "out1", 1.0);
        agent.RecordInteraction("in2", "out2", 0.0);
        var perf = agent.GetPerformance();

        // Assert
        // EMA: first = 1.0, second = 0.5 * 0.0 + 0.5 * 1.0 = 0.5
        perf.AverageResponseQuality.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void ShouldAdapt_WithTooFewInteractions_ReturnsFalse()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 50));
        agent.RecordInteraction("in", "out", 0.5);

        // Act & Assert
        agent.ShouldAdapt().Should().BeFalse();
    }

    [Fact]
    public void Adapt_WithTooFewInteractions_ReturnsFailure()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 50));
        agent.RecordInteraction("in", "out", 0.5);

        // Act
        var result = agent.Adapt();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient");
    }

    [Fact]
    public void Adapt_WithSufficientInteractions_ReturnsSuccess()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 5));
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);

        // Act
        var result = agent.Adapt();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentId.Should().Be(agent.AgentId);
    }

    [Fact]
    public void Adapt_AddsToAdaptationHistory()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 5));
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);

        // Act
        agent.Adapt();

        // Assert
        agent.GetAdaptationHistory().Should().HaveCount(1);
    }

    [Fact]
    public void Adapt_UpdatesCurrentStrategy()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 5));
        var originalStrategy = agent.GetCurrentStrategy();
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);

        // Act
        agent.Adapt();

        // Assert
        var newStrategy = agent.GetCurrentStrategy();
        // The strategy should have changed in some way
        (newStrategy.LearningRate != originalStrategy.LearningRate ||
         newStrategy.ExplorationRate != originalStrategy.ExplorationRate)
            .Should().BeTrue();
    }

    [Fact]
    public void GetPerformance_ReturnsCurrentSnapshot()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();
        agent.RecordInteraction("in", "out", 0.7);

        // Act
        var perf = agent.GetPerformance();

        // Assert
        perf.TotalInteractions.Should().Be(1);
        perf.AgentId.Should().Be(agent.AgentId);
    }

    [Fact]
    public void GetAdaptationHistory_InitiallyEmpty()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act & Assert
        agent.GetAdaptationHistory().Should().BeEmpty();
    }

    [Fact]
    public void Rollback_WithNonExistentId_ReturnsFailure()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.Rollback(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void Rollback_WithValidId_ReturnsRollbackEvent()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 5));
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);
        var adaptResult = agent.Adapt();
        var adaptationId = adaptResult.Value.Id;

        // Act
        var rollbackResult = agent.Rollback(adaptationId);

        // Assert
        rollbackResult.IsSuccess.Should().BeTrue();
        rollbackResult.Value.EventType.Should().Be(AdaptationEventType.Rollback);
    }

    [Fact]
    public void Rollback_RestoresPreviousStrategy()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 5));
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);
        var beforeStrategy = agent.GetCurrentStrategy();
        var adaptResult = agent.Adapt();

        // Act
        agent.Rollback(adaptResult.Value.Id);

        // Assert
        var afterRollback = agent.GetCurrentStrategy();
        afterRollback.Id.Should().Be(beforeStrategy.Id);
    }

    [Fact]
    public void Rollback_AddsRollbackEventToHistory()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(MinInteractionsBeforeAdaptation: 5));
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);
        var adaptResult = agent.Adapt();

        // Act
        agent.Rollback(adaptResult.Value.Id);

        // Assert
        var history = agent.GetAdaptationHistory();
        history.Should().HaveCount(2); // original + rollback
        history.Last().EventType.Should().Be(AdaptationEventType.Rollback);
    }

    [Fact]
    public void GetCurrentStrategy_ReturnsDefaultInitially()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var strategy = agent.GetCurrentStrategy();

        // Assert
        strategy.Name.Should().Be("Default");
    }

    [Fact]
    public void Adapt_TrimsHistoryWhenExceedingMax()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent(
            config: new AdaptiveAgentConfig(
                MinInteractionsBeforeAdaptation: 1,
                MaxAdaptationHistory: 3));
        for (int i = 0; i < 5; i++)
        {
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);
            agent.Adapt();
        }

        // Assert
        agent.GetAdaptationHistory().Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void RecordInteraction_RecordsLearningCurvePeriodically()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act - record 10 interactions (curve recorded at every 10th)
        for (int i = 0; i < 10; i++)
            agent.RecordInteraction($"in{i}", $"out{i}", 0.5);

        // Assert
        var perf = agent.GetPerformance();
        perf.LearningCurve.Should().HaveCount(1);
    }
}
