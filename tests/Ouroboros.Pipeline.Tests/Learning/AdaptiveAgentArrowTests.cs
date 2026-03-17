using NSubstitute;
using Ouroboros.Pipeline.Learning;
using Unit = Ouroboros.Abstractions.Unit;

namespace Ouroboros.Tests.Learning;

public class AdaptiveAgentArrowTests
{
    private readonly IAdaptiveAgent _agent = Substitute.For<IAdaptiveAgent>();

    [Fact]
    public void RecordInteractionStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.RecordInteractionStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RecordInteractionStep_DelegatesToAgent()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        _agent.RecordInteraction("in", "out", 0.5)
            .Returns(Result<AgentPerformance, string>.Success(performance));
        var step = AdaptiveAgentArrow.RecordInteractionStep(_agent);

        // Act
        var result = await step(("in", "out", 0.5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        _agent.Received(1).RecordInteraction("in", "out", 0.5);
    }

    [Fact]
    public void TryAdaptStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.TryAdaptStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task TryAdaptStep_WhenShouldNotAdapt_ReturnsNone()
    {
        // Arrange
        _agent.ShouldAdapt().Returns(false);
        var step = AdaptiveAgentArrow.TryAdaptStep(_agent);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task TryAdaptStep_WhenShouldAdaptAndSucceeds_ReturnsSome()
    {
        // Arrange
        _agent.ShouldAdapt().Returns(true);
        var adaptEvent = AdaptationEvent.Create(
            Guid.NewGuid(), AdaptationEventType.StrategyChange,
            "Test", AgentPerformance.Initial(Guid.NewGuid()));
        _agent.Adapt().Returns(Result<AdaptationEvent, string>.Success(adaptEvent));
        var step = AdaptiveAgentArrow.TryAdaptStep(_agent);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public async Task TryAdaptStep_WhenAdaptFails_ReturnsNone()
    {
        // Arrange
        _agent.ShouldAdapt().Returns(true);
        _agent.Adapt().Returns(Result<AdaptationEvent, string>.Failure("Failed"));
        var step = AdaptiveAgentArrow.TryAdaptStep(_agent);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void GetPerformanceStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.GetPerformanceStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetPerformanceStep_ReturnsPerformance()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        _agent.GetPerformance().Returns(performance);
        var step = AdaptiveAgentArrow.GetPerformanceStep(_agent);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.Should().Be(performance);
    }

    [Fact]
    public void GetAdaptationHistoryStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.GetAdaptationHistoryStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetAdaptationHistoryStep_ReturnsHistory()
    {
        // Arrange
        var history = ImmutableList<AdaptationEvent>.Empty;
        _agent.GetAdaptationHistory().Returns(history);
        var step = AdaptiveAgentArrow.GetAdaptationHistoryStep(_agent);

        // Act
        var result = await step(Unit.Value);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void RollbackStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.RollbackStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RollbackStep_DelegatesToAgent()
    {
        // Arrange
        var adaptationId = Guid.NewGuid();
        var rollbackEvent = AdaptationEvent.Create(
            Guid.NewGuid(), AdaptationEventType.Rollback,
            "Rollback", AgentPerformance.Initial(Guid.NewGuid()));
        _agent.Rollback(adaptationId)
            .Returns(Result<AdaptationEvent, string>.Success(rollbackEvent));
        var step = AdaptiveAgentArrow.RollbackStep(_agent);

        // Act
        var result = await step(adaptationId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _agent.Received(1).Rollback(adaptationId);
    }

    [Fact]
    public void FullLearningPipeline_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.FullLearningPipeline(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task FullLearningPipeline_RecordsInteractionAndChecksAdaptation()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        _agent.RecordInteraction("in", "out", 0.5)
            .Returns(Result<AgentPerformance, string>.Success(performance));
        _agent.ShouldAdapt().Returns(false);
        var step = AdaptiveAgentArrow.FullLearningPipeline(_agent);

        // Act
        var result = await step(("in", "out", 0.5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Performance.Should().Be(performance);
        result.Value.Adaptation.Should().BeNull();
    }

    [Fact]
    public async Task FullLearningPipeline_WithAdaptation_ReturnsAdaptationEvent()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        var adaptEvent = AdaptationEvent.Create(
            Guid.NewGuid(), AdaptationEventType.ParameterTune,
            "Tune", performance);
        _agent.RecordInteraction("in", "out", 0.5)
            .Returns(Result<AgentPerformance, string>.Success(performance));
        _agent.ShouldAdapt().Returns(true);
        _agent.Adapt().Returns(Result<AdaptationEvent, string>.Success(adaptEvent));
        var step = AdaptiveAgentArrow.FullLearningPipeline(_agent);

        // Act
        var result = await step(("in", "out", 0.5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Adaptation.Should().NotBeNull();
    }

    [Fact]
    public async Task FullLearningPipeline_WhenRecordFails_ReturnsFailure()
    {
        // Arrange
        _agent.RecordInteraction(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns(Result<AgentPerformance, string>.Failure("Error"));
        var step = AdaptiveAgentArrow.FullLearningPipeline(_agent);

        // Act
        var result = await step(("in", "out", 0.5));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ProcessBatchStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.ProcessBatchStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessBatchStep_ProcessesAllInteractions()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        _agent.RecordInteraction(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>())
            .Returns(Result<AgentPerformance, string>.Success(performance));
        var step = AdaptiveAgentArrow.ProcessBatchStep(_agent);
        var interactions = new[]
        {
            ("in1", "out1", 0.5),
            ("in2", "out2", 0.7),
        };

        // Act
        var result = await step(interactions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _agent.Received(2).RecordInteraction(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<double>());
    }

    [Fact]
    public async Task ProcessBatchStep_WithEmptyBatch_ReturnsFailure()
    {
        // Arrange
        var step = AdaptiveAgentArrow.ProcessBatchStep(_agent);

        // Act
        var result = await step(Array.Empty<(string, string, double)>());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ConditionalAdaptStep_WithNullAgent_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.ConditionalAdaptStep(null!, _ => true);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConditionalAdaptStep_WithNullPredicate_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => AdaptiveAgentArrow.ConditionalAdaptStep(_agent, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ConditionalAdaptStep_WhenPredicateFalse_ReturnsNone()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        var step = AdaptiveAgentArrow.ConditionalAdaptStep(_agent, _ => false);

        // Act
        var result = await step(performance);

        // Assert
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task ConditionalAdaptStep_WhenPredicateTrueAndShouldAdapt_ReturnsSome()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        var adaptEvent = AdaptationEvent.Create(
            Guid.NewGuid(), AdaptationEventType.StrategyChange,
            "Test", performance);
        _agent.ShouldAdapt().Returns(true);
        _agent.Adapt().Returns(Result<AdaptationEvent, string>.Success(adaptEvent));
        var step = AdaptiveAgentArrow.ConditionalAdaptStep(_agent, _ => true);

        // Act
        var result = await step(performance);

        // Assert
        result.IsSome.Should().BeTrue();
    }
}
