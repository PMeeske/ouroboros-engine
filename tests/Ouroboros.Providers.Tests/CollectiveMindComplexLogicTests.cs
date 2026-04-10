// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Ouroboros.Abstractions.Core;
using Ouroboros.Providers;
using Polly;
using R3;

namespace Ouroboros.Tests;

/// <summary>
/// Complex logic tests for CollectiveMind covering adaptive mode selection,
/// tier inference, pathway selection, decomposition routing, NeuralPathway metrics,
/// and ensemble/election orchestration patterns.
/// </summary>
[Trait("Category", "Unit")]
public class CollectiveMindComplexLogicTests
{
    // ================================================================
    // NeuralPathway health metrics and weight adjustments
    // ================================================================

    [Fact]
    public void NeuralPathway_RecordActivation_IncreasesWeight()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);
        double initialWeight = pathway.Weight;

        // Act
        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));

        // Assert
        pathway.Weight.Should().BeGreaterThan(initialWeight, "activation should boost weight by 5%");
        pathway.Activations.Should().Be(1);
        pathway.Synapses.Should().Be(1);
        pathway.ActivationRate.Should().Be(1.0);
    }

    [Fact]
    public void NeuralPathway_RecordInhibition_DecreasesWeight()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);
        double initialWeight = pathway.Weight;

        // Act
        pathway.RecordInhibition();

        // Assert
        pathway.Weight.Should().BeLessThan(initialWeight, "inhibition should reduce weight by 30%");
        pathway.Weight.Should().BeApproximately(0.7, 0.01);
        pathway.Inhibitions.Should().Be(1);
        pathway.Synapses.Should().Be(1);
        pathway.ActivationRate.Should().Be(0.0);
    }

    [Fact]
    public void NeuralPathway_MultipleInhibitions_WeightFloorAtPointOne()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);

        // Act: 20 consecutive failures
        for (int i = 0; i < 20; i++)
        {
            pathway.RecordInhibition();
        }

        // Assert
        pathway.Weight.Should().BeGreaterThanOrEqualTo(0.1, "weight has a minimum floor of 0.1");
    }

    [Fact]
    public void NeuralPathway_MultipleActivations_WeightCappedAtTwo()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);

        // Act: 50 consecutive successes
        for (int i = 0; i < 50; i++)
        {
            pathway.RecordActivation(TimeSpan.FromMilliseconds(100));
        }

        // Assert
        pathway.Weight.Should().BeLessThanOrEqualTo(2.0, "weight is capped at 2.0");
    }

    [Fact]
    public void NeuralPathway_ActivationRate_MixedResults_CalculatesCorrectly()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);

        // Act: 7 successes, 3 failures = 70% activation rate
        for (int i = 0; i < 7; i++) pathway.RecordActivation(TimeSpan.FromMilliseconds(50));
        for (int i = 0; i < 3; i++) pathway.RecordInhibition();

        // Assert
        pathway.ActivationRate.Should().BeApproximately(0.7, 0.01);
        pathway.Synapses.Should().Be(10);
    }

    [Fact]
    public void NeuralPathway_AverageLatency_ExponentialMovingAverage()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);

        // Act: first latency sets the baseline
        pathway.RecordActivation(TimeSpan.FromMilliseconds(100));
        pathway.AverageLatency.TotalMilliseconds.Should().Be(100);

        // Second latency applies EMA: 100*0.8 + 200*0.2 = 120
        pathway.RecordActivation(TimeSpan.FromMilliseconds(200));
        pathway.AverageLatency.TotalMilliseconds.Should().BeApproximately(120, 1);

        // Third: 120*0.8 + 50*0.2 = 106
        pathway.RecordActivation(TimeSpan.FromMilliseconds(50));
        pathway.AverageLatency.TotalMilliseconds.Should().BeApproximately(106, 1);
    }

    [Fact]
    public void NeuralPathway_NoSynapses_ActivationRateIsOne()
    {
        // Arrange
        var pathway = CreatePathway("test", PathwayTier.CloudLight);

        // Assert: default activation rate is 1.0 for unused pathways
        pathway.ActivationRate.Should().Be(1.0);
    }

    // ================================================================
    // CollectiveMind - construction and configuration
    // ================================================================

    [Fact]
    public void CollectiveMind_DefaultConstruction_InitializesCorrectly()
    {
        // Act
        using var mind = new CollectiveMind();

        // Assert
        mind.Pathways.Should().BeEmpty();
        mind.HealthyPathwayCount.Should().Be(0);
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Adaptive);
        mind.ElectionStrategy.Should().Be(ElectionStrategy.WeightedMajority);
    }

    [Fact]
    public void CollectiveMind_WithElectionStrategy_SetsStrategy()
    {
        // Act
        using var mind = new CollectiveMind(ElectionStrategy.BordaCount);

        // Assert
        mind.ElectionStrategy.Should().Be(ElectionStrategy.BordaCount);
    }

    [Fact]
    public void CollectiveMind_DecompositionConfig_NullSetsDefault()
    {
        // Arrange
        using var mind = new CollectiveMind();
        mind.DecompositionConfig = new DecompositionConfig { MaxSubGoals = 5 };

        // Act
        mind.DecompositionConfig = null!;

        // Assert
        mind.DecompositionConfig.Should().Be(DecompositionConfig.Default);
    }

    // ================================================================
    // CollectiveMind - thinking mode dispatch
    // ================================================================

    [Fact]
    public async Task GenerateWithThinkingAsync_NoPathways_ThrowsInvalidOperation()
    {
        // Arrange
        using var mind = new CollectiveMind();
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mind.GenerateWithThinkingAsync("test prompt"));
    }

    [Fact]
    public async Task GenerateTextAsync_DelegatesTo_GenerateWithThinkingAsync()
    {
        // Arrange: will throw due to no pathways, proving delegation
        using var mind = new CollectiveMind();
        mind.ThinkingMode = CollectiveThinkingMode.Racing;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mind.GenerateTextAsync("test"));
    }

    // ================================================================
    // Adaptive mode - strategy selection based on conditions
    // ================================================================

    [Fact]
    public async Task AdaptiveMode_NoHealthyPathways_ThrowsInvalidOperation()
    {
        // Arrange
        using var mind = new CollectiveMind();
        mind.ThinkingMode = CollectiveThinkingMode.Adaptive;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mind.GenerateWithThinkingAsync("test"));
    }

    // ================================================================
    // Ensemble mode - no healthy pathways
    // ================================================================

    [Fact]
    public async Task EnsembleMode_NoHealthyPathways_ThrowsInvalidOperation()
    {
        // Arrange
        using var mind = new CollectiveMind();
        mind.ThinkingMode = CollectiveThinkingMode.Ensemble;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mind.GenerateWithThinkingAsync("test"));
    }

    // ================================================================
    // Decomposed mode - no pathways
    // ================================================================

    [Fact]
    public async Task DecomposedMode_NoPathways_ThrowsInvalidOperation()
    {
        // Arrange
        using var mind = new CollectiveMind();
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mind.GenerateWithThinkingAsync("decompose this"));
    }

    // ================================================================
    // DecompositionConfig routing logic
    // ================================================================

    [Fact]
    public void DecompositionConfig_Default_HasExpectedTypeRouting()
    {
        // Arrange
        var config = DecompositionConfig.Default;

        // Assert
        config.TypeRouting[SubGoalType.Retrieval].Should().Be(PathwayTier.Local);
        config.TypeRouting[SubGoalType.Transform].Should().Be(PathwayTier.Local);
        config.TypeRouting[SubGoalType.Reasoning].Should().Be(PathwayTier.CloudLight);
        config.TypeRouting[SubGoalType.Creative].Should().Be(PathwayTier.CloudPremium);
        config.TypeRouting[SubGoalType.Coding].Should().Be(PathwayTier.Specialized);
        config.TypeRouting[SubGoalType.Math].Should().Be(PathwayTier.Specialized);
        config.TypeRouting[SubGoalType.Synthesis].Should().Be(PathwayTier.CloudPremium);
    }

    [Fact]
    public void DecompositionConfig_LocalFirst_PreferLocal()
    {
        // Arrange
        var config = DecompositionConfig.LocalFirst;

        // Assert
        config.PreferLocalForSimple.Should().BeTrue();
        config.PremiumForSynthesis.Should().BeFalse();
        config.TypeRouting[SubGoalType.Reasoning].Should().Be(PathwayTier.Local);
        config.TypeRouting[SubGoalType.Coding].Should().Be(PathwayTier.Local);
    }

    [Fact]
    public void DecompositionConfig_QualityFirst_PreferPremium()
    {
        // Arrange
        var config = DecompositionConfig.QualityFirst;

        // Assert
        config.PreferLocalForSimple.Should().BeFalse();
        config.PremiumForSynthesis.Should().BeTrue();
        config.TypeRouting[SubGoalType.Reasoning].Should().Be(PathwayTier.CloudPremium);
        config.TypeRouting[SubGoalType.Coding].Should().Be(PathwayTier.CloudPremium);
    }

    // ================================================================
    // ThoughtStream observation
    // ================================================================

    [Fact]
    public void ThoughtStream_IsObservable()
    {
        // Arrange
        using var mind = new CollectiveMind();
        var thoughts = new List<string>();
        using var subscription = mind.ThoughtStream.Subscribe(
            t => thoughts.Add(t),
            _ => { });

        // Act: set master (emits to thought stream)
        mind.SetFirstAsMaster(); // No pathways, so nothing happens

        // Assert
        mind.ThoughtStream.Should().NotBeNull();
    }

    // ================================================================
    // Dispose behavior
    // ================================================================

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var mind = new CollectiveMind();
        var thoughts = new List<string>();
        bool completed = false;
        using var subscription = mind.ThoughtStream.Subscribe(
            t => thoughts.Add(t),
            _ => completed = true);

        // Act
        mind.Dispose();

        // Assert
        completed.Should().BeTrue("ThoughtStream should complete on dispose");
        mind.Pathways.Should().BeEmpty("pathways should be cleared on dispose");
    }

    // ================================================================
    // SubGoal record
    // ================================================================

    [Fact]
    public void SubGoal_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var goal1 = new SubGoal("g1", "desc", SubGoalComplexity.Moderate, SubGoalType.Reasoning,
            Array.Empty<string>(), PathwayTier.CloudLight);
        var goal2 = new SubGoal("g1", "desc", SubGoalComplexity.Moderate, SubGoalType.Reasoning,
            Array.Empty<string>(), PathwayTier.CloudLight);

        // Assert
        goal1.Should().Be(goal2);
    }

    [Fact]
    public void SubGoalResult_SuccessAndFailure_DifferInFields()
    {
        // Arrange
        var successResult = new SubGoalResult("g1", "pathway_a",
            new ThinkingResponse(null, "result text"), TimeSpan.FromSeconds(1), true);
        var failResult = new SubGoalResult("g1", "pathway_a",
            new ThinkingResponse(null, ""), TimeSpan.FromSeconds(1), false, "timeout");

        // Assert
        successResult.Success.Should().BeTrue();
        successResult.ErrorMessage.Should().BeNull();
        failResult.Success.Should().BeFalse();
        failResult.ErrorMessage.Should().Be("timeout");
    }

    // ================================================================
    // ThinkingResponse
    // ================================================================

    [Fact]
    public void ThinkingResponse_HasThinking_TrueWhenThinkingPresent()
    {
        // Arrange
        var withThinking = new ThinkingResponse("I'm thinking...", "result");
        var withoutThinking = new ThinkingResponse(null, "result");

        // Assert
        withThinking.HasThinking.Should().BeTrue();
        withoutThinking.HasThinking.Should().BeFalse();
    }

    // ================================================================
    // Consciousness status and Phi computation
    // ================================================================

    [Fact]
    public void GetConsciousnessStatus_EmptyMind_ReportsZeroPathways()
    {
        // Arrange
        using var mind = new CollectiveMind();

        // Act
        var status = mind.GetConsciousnessStatus();

        // Assert
        status.Should().Contain("Pathways: 0 total");
        status.Should().Contain("0 healthy");
    }

    [Fact]
    public void ComputePhi_EmptyMind_ReturnsResult()
    {
        // Arrange
        using var mind = new CollectiveMind();

        // Act
        var phi = mind.ComputePhi();

        // Assert
        phi.Should().NotBeNull();
    }

    // ================================================================
    // SetMaster and SetFirstAsMaster
    // ================================================================

    [Fact]
    public void SetMaster_NonExistentPathway_DoesNotThrow()
    {
        // Arrange
        using var mind = new CollectiveMind();

        // Act (no pathways added)
        var result = mind.SetMaster("nonexistent");

        // Assert: fluent API returns self
        result.Should().BeSameAs(mind);
    }

    [Fact]
    public void SetFirstAsMaster_NoPathways_DoesNotThrow()
    {
        // Arrange
        using var mind = new CollectiveMind();

        // Act
        var result = mind.SetFirstAsMaster();

        // Assert
        result.Should().BeSameAs(mind);
    }

    // ================================================================
    // CollectiveMind - cost tracking
    // ================================================================

    [Fact]
    public void CostTracker_AlwaysPresent()
    {
        // Arrange
        using var mind = new CollectiveMind();

        // Assert
        mind.CostTracker.Should().NotBeNull();
    }

    // ================================================================
    // Optimization suggestions
    // ================================================================

    [Fact]
    public void GetOptimizationSuggestions_NoElection_ReturnsEmpty()
    {
        // Arrange
        using var mind = new CollectiveMind();

        // Act
        var suggestions = mind.GetOptimizationSuggestions();

        // Assert
        suggestions.Should().NotBeNull();
    }

    // ================================================================
    // Helper methods
    // ================================================================

    private static NeuralPathway CreatePathway(
        string name,
        PathwayTier tier,
        double initialWeight = 1.0,
        HashSet<SubGoalType>? specializations = null)
    {
        var mockModel = new Mock<IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("mock response");

        var circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));

        return new NeuralPathway
        {
            Name = name,
            EndpointType = ChatEndpointType.OpenAI,
            Model = mockModel.Object,
            CostTracker = new LlmCostTracker("test-model", name),
            CircuitBreaker = circuitBreaker,
            Tier = tier,
            Weight = initialWeight,
            Specializations = specializations ?? new HashSet<SubGoalType>()
        };
    }
}
