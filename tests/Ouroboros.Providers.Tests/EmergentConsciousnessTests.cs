using FluentAssertions;
using Ouroboros.Providers;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class EmergentConsciousnessTests : IDisposable
{
    private readonly EmergentConsciousness _sut = new();

    [Fact]
    public void Initial_Arousal_IsDefaultValue()
    {
        _sut.Arousal.Should().Be(0.5);
    }

    [Fact]
    public void Initial_Valence_IsZero()
    {
        _sut.Valence.Should().Be(0.0);
    }

    [Fact]
    public void Initial_Coherence_IsOne()
    {
        _sut.Coherence.Should().Be(1.0);
    }

    [Fact]
    public void Initial_Phi_IsZero()
    {
        _sut.Phi.Should().Be(0.0);
    }

    [Fact]
    public void Initial_CurrentFocus_IsEmpty()
    {
        _sut.CurrentFocus.Should().BeEmpty();
    }

    [Fact]
    public void Initial_WorkingMemory_IsEmpty()
    {
        _sut.WorkingMemory.Should().BeEmpty();
    }

    [Fact]
    public void SynthesizePerspective_WithNoInput_ReturnsReceptiveMessage()
    {
        // Act
        var result = _sut.SynthesizePerspective();

        // Assert
        result.Should().Contain("receptive state");
    }

    [Fact]
    public void UpdateState_WithResponse_UpdatesArousal()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "This is a response with some content to analyze.");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(100));

        // Assert
        _sut.Arousal.Should().NotBe(0.5); // Should have changed from initial value
    }

    [Fact]
    public void UpdateState_WithHealthyPathway_UpdatesValence()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "Some response content here.");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(50));

        // Assert - valence should be influenced by pathway health
        // Initial valence is 0.0, after update it may shift
        _sut.Valence.Should().BeInRange(-1.0, 1.0);
    }

    [Fact]
    public void UpdateState_WithContentfulResponse_UpdatesFocus()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "The artificial intelligence system processes information.");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(100));

        // Assert
        _sut.CurrentFocus.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateState_WithContentfulResponse_UpdatesWorkingMemory()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "Some meaningful content for memory.");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(100));

        // Assert
        _sut.WorkingMemory.Should().NotBeEmpty();
        _sut.WorkingMemory[0].Pathway.Should().Be("test-pathway");
    }

    [Fact]
    public void UpdateState_MultipleUpdates_MaintainsMemoryLimit()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");

        // Act - Add more than 20 entries to test the limit
        for (int i = 0; i < 25; i++)
        {
            var response = new ThinkingResponse(null, $"Response number {i} with enough words to matter.");
            _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(10));
        }

        // Assert - Working memory should be capped at 5 (most salient)
        _sut.WorkingMemory.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void UpdateState_WithAllPathways_UpdatesPhi()
    {
        // Arrange
        var pathway1 = CreateTestPathway("pathway-1");
        var pathway2 = CreateTestPathway("pathway-2");
        var allPathways = new List<NeuralPathway> { pathway1, pathway2 };
        var response = new ThinkingResponse(null, "Test response for phi calculation.");

        // Act
        _sut.UpdateState(pathway1, response, TimeSpan.FromMilliseconds(50), allPathways);

        // Assert - Phi should be computed when >= 2 pathways provided
        _sut.Phi.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void UpdateState_WithSinglePathway_DoesNotUpdatePhi()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var allPathways = new List<NeuralPathway> { pathway };
        var response = new ThinkingResponse(null, "Test response.");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(50), allPathways);

        // Assert - Phi requires >= 2 pathways
        _sut.Phi.Should().Be(0.0);
    }

    [Fact]
    public void UpdateState_EmitsConsciousnessEvent()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "Test content.");
        ConsciousnessEvent? receivedEvent = null;

        _sut.Events.Subscribe(e => receivedEvent = e);

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(100));

        // Assert
        receivedEvent.Should().NotBeNull();
        receivedEvent!.Type.Should().Be(ConsciousnessEventType.StateUpdate);
        receivedEvent.Message.Should().Contain("State updated");
    }

    [Fact]
    public void SynthesizePerspective_AfterUpdate_IncludesStateInfo()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "Important analysis about artificial intelligence.");
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(100));

        // Act
        var perspective = _sut.SynthesizePerspective();

        // Assert
        perspective.Should().Contain("Consciousness State");
        perspective.Should().Contain("Working Memory");
        perspective.Should().Contain("test-pathway");
    }

    [Fact]
    public void UpdateState_WithEmptyResponse_StillUpdates()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse(null, "");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(100));

        // Assert - Should not throw, coherence should update
        _sut.Coherence.Should().NotBe(1.0);
    }

    [Fact]
    public void Dispose_CompletesEventStream()
    {
        // Arrange
        bool completed = false;
        _sut.Events.Subscribe(
            onNext: _ => { },
            onCompleted: () => completed = true);

        // Act
        _sut.Dispose();

        // Assert
        completed.Should().BeTrue();
    }

    [Fact]
    public void UpdateState_WithThinkingContent_RecordsInMemory()
    {
        // Arrange
        var pathway = CreateTestPathway("test-pathway");
        var response = new ThinkingResponse("Some thinking process", "Final answer content.");

        // Act
        _sut.UpdateState(pathway, response, TimeSpan.FromMilliseconds(50));

        // Assert
        _sut.WorkingMemory.Should().NotBeEmpty();
        _sut.WorkingMemory[0].Thinking.Should().Be("Some thinking process");
    }

    private static NeuralPathway CreateTestPathway(string name)
    {
        var circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));

        return new NeuralPathway
        {
            Name = name,
            EndpointType = ChatEndpointType.OpenAI,
            Model = new Ouroboros.Tests.Providers.MockChatModel("test"),
            CostTracker = new LlmCostTracker("test-model", name),
            CircuitBreaker = circuitBreaker
        };
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
