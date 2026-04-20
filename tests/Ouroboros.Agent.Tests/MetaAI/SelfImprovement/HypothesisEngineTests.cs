// <copyright file="HypothesisEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;
using MetaAIHypothesis = Ouroboros.Agent.MetaAI.Hypothesis;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

/// <summary>
/// Tests for the HypothesisEngine core logic including generation, testing,
/// Bayesian updating, and domain queries. Complements HypothesisEngineExtendedTests
/// which covers null/edge-case validation.
/// </summary>
[Trait("Category", "Unit")]
public class HypothesisEngineTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<IMetaAIPlannerOrchestrator> _orchestratorMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();
    private readonly Mock<Ouroboros.Core.Ethics.IEthicsFramework> _ethicsMock = new();

    // ── Constructor validation ──────────────────────────────────────

    [Fact]
    public void Constructor_NullMemory_Throws()
    {
        var act = () => new HypothesisEngine(_llmMock.Object, _orchestratorMock.Object, null!, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullEthics_Throws()
    {
        var act = () => new HypothesisEngine(_llmMock.Object, _orchestratorMock.Object, _memoryMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_AllValid_DoesNotThrow()
    {
        var act = () => new HypothesisEngine(_llmMock.Object, _orchestratorMock.Object, _memoryMock.Object, _ethicsMock.Object);
        act.Should().NotThrow();
    }

    // ── GenerateHypothesisAsync ─────────────────────────────────────

    [Fact]
    public async Task GenerateHypothesisAsync_ValidObservation_ReturnsHypothesis()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("no experiences"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: The system slows under load\nCONFIDENCE: 0.7\nDOMAIN: performance\nEVIDENCE: Observed latency spikes");

        var engine = CreateEngine();

        // Act
        var result = await engine.GenerateHypothesisAsync("System latency increases during peak hours");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Statement.Should().Be("The system slows under load");
        result.Value.Domain.Should().Be("performance");
        result.Value.Confidence.Should().BeApproximately(0.7, 0.01);
        result.Value.SupportingEvidence.Should().NotBeEmpty();
        result.Value.Tested.Should().BeFalse();
        result.Value.Validated.Should().BeNull();
    }

    [Fact]
    public async Task GenerateHypothesisAsync_LlmThrows_ReturnsFailure()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("no experiences"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var engine = CreateEngine();

        // Act
        var result = await engine.GenerateHypothesisAsync("observation");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Hypothesis generation failed");
    }

    [Fact]
    public async Task GenerateHypothesisAsync_StoresHypothesisForLaterRetrieval()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Test hypothesis\nCONFIDENCE: 0.6\nDOMAIN: testing\nEVIDENCE: Unit tests");

        var engine = CreateEngine();

        // Act
        await engine.GenerateHypothesisAsync("observation");

        // Assert — hypothesis should be retrievable by domain
        var hypotheses = engine.GetHypothesesByDomain("testing");
        hypotheses.Should().HaveCount(1);
        hypotheses[0].Statement.Should().Be("Test hypothesis");
    }

    [Fact]
    public async Task GenerateHypothesisAsync_ConfidenceClampedToValidRange()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Test\nCONFIDENCE: 1.5\nDOMAIN: test\nEVIDENCE: None");

        var engine = CreateEngine();

        // Act
        var result = await engine.GenerateHypothesisAsync("observation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeLessThanOrEqualTo(1.0);
    }

    // ── DesignExperimentAsync ───────────────────────────────────────

    [Fact]
    public async Task DesignExperimentAsync_ValidHypothesis_ReturnsExperiment()
    {
        // Arrange
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("STEP 1: Measure baseline\nEXPECTED_IF_TRUE: Performance improves\nSTEP 2: Apply optimization\nEXPECTED_IF_FALSE: No change\nCRITERIA: 20% improvement threshold");

        var engine = CreateEngine();
        var hypothesis = CreateTestHypothesis();

        // Act
        var result = await engine.DesignExperimentAsync(hypothesis);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Value.ExpectedOutcomes.Should().NotBeEmpty();
        result.Value.Description.Should().Contain(hypothesis.Statement);
    }

    [Fact]
    public async Task DesignExperimentAsync_LlmThrows_ReturnsFailure()
    {
        // Arrange
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var engine = CreateEngine();

        // Act
        var result = await engine.DesignExperimentAsync(CreateTestHypothesis());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Experiment design failed");
    }

    // ── TestHypothesisAsync ─────────────────────────────────────────

    [Fact]
    public async Task TestHypothesisAsync_EthicsRejectsResearch_ReturnsFailure()
    {
        // Arrange
        _ethicsMock.Setup(e => e.EvaluateResearchAsync(
                It.IsAny<string>(),
                It.IsAny<Ouroboros.Core.Ethics.ActionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Ouroboros.Core.Ethics.EthicalClearance, string>.Failure("Research violates ethics"));

        var engine = CreateEngine();

        // Act
        var result = await engine.TestHypothesisAsync(CreateTestHypothesis(), CreateTestExperiment());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ethics");
    }

    [Fact]
    public async Task TestHypothesisAsync_EthicsNotPermitted_ReturnsFailure()
    {
        // Arrange
        var clearance = new Ouroboros.Core.Ethics.EthicalClearance
        {
            IsPermitted = false,
            Level = Ouroboros.Core.Ethics.EthicalClearanceLevel.Prohibited,
            Reasoning = "Dangerous research"
        };
        _ethicsMock.Setup(e => e.EvaluateResearchAsync(
                It.IsAny<string>(),
                It.IsAny<Ouroboros.Core.Ethics.ActionContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Ouroboros.Core.Ethics.EthicalClearance, string>.Success(clearance));

        var engine = CreateEngine();

        // Act
        var result = await engine.TestHypothesisAsync(CreateTestHypothesis(), CreateTestExperiment());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("rejected");
    }

    // ── UpdateHypothesis ────────────────────────────────────────────

    [Fact]
    public async Task UpdateHypothesis_SupportingEvidence_IncreasesConfidence()
    {
        // Arrange — first create a hypothesis via generation
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Test\nCONFIDENCE: 0.5\nDOMAIN: test\nEVIDENCE: Initial");

        var engine = CreateEngine();
        var genResult = await engine.GenerateHypothesisAsync("observation");
        genResult.IsSuccess.Should().BeTrue();

        var initialConfidence = genResult.Value.Confidence;

        // Act — add supporting evidence
        engine.UpdateHypothesis(genResult.Value.Id, "New supporting data", true);

        // Assert
        var updated = engine.GetHypothesesByDomain("test");
        updated.Should().HaveCount(1);
        updated[0].Confidence.Should().BeGreaterThan(initialConfidence);
        updated[0].SupportingEvidence.Should().Contain("New supporting data");
    }

    [Fact]
    public async Task UpdateHypothesis_CounterEvidence_DecreasesConfidence()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Test\nCONFIDENCE: 0.5\nDOMAIN: test\nEVIDENCE: Initial");

        var engine = CreateEngine();
        var genResult = await engine.GenerateHypothesisAsync("observation");
        var initialConfidence = genResult.Value.Confidence;

        // Act — add counter evidence
        engine.UpdateHypothesis(genResult.Value.Id, "Contradicting data", false);

        // Assert
        var updated = engine.GetHypothesesByDomain("test");
        updated[0].Confidence.Should().BeLessThan(initialConfidence);
        updated[0].CounterEvidence.Should().Contain("Contradicting data");
    }

    // ── GetConfidenceTrend ──────────────────────────────────────────

    [Fact]
    public async Task GetConfidenceTrend_AfterGenerationAndUpdate_TracksTrend()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Test\nCONFIDENCE: 0.5\nDOMAIN: test\nEVIDENCE: Initial");

        var engine = CreateEngine();
        var genResult = await engine.GenerateHypothesisAsync("observation");

        // Act — update to add a second trend point
        engine.UpdateHypothesis(genResult.Value.Id, "evidence", true);

        // Assert
        var trend = engine.GetConfidenceTrend(genResult.Value.Id);
        trend.Should().HaveCount(2);
        trend[0].confidence.Should().Be(0.5);
        trend[1].confidence.Should().BeGreaterThan(0.5);
    }

    // ── GetHypothesesByDomain ───────────────────────────────────────

    [Fact]
    public async Task GetHypothesesByDomain_MultipleHypotheses_SortedByConfidenceDescending()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        var engine = CreateEngine();

        // Generate two hypotheses in same domain with different confidences
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Low confidence\nCONFIDENCE: 0.3\nDOMAIN: shared\nEVIDENCE: weak");
        await engine.GenerateHypothesisAsync("obs1");

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: High confidence\nCONFIDENCE: 0.9\nDOMAIN: shared\nEVIDENCE: strong");
        await engine.GenerateHypothesisAsync("obs2");

        // Act
        var hypotheses = engine.GetHypothesesByDomain("shared");

        // Assert
        hypotheses.Should().HaveCount(2);
        hypotheses[0].Confidence.Should().BeGreaterThan(hypotheses[1].Confidence);
    }

    [Fact]
    public async Task GetHypothesesByDomain_PartialDomainMatch_ReturnsMatches()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Test\nCONFIDENCE: 0.5\nDOMAIN: machine learning\nEVIDENCE: data");

        var engine = CreateEngine();
        await engine.GenerateHypothesisAsync("observation");

        // Act — partial match
        var results = engine.GetHypothesesByDomain("machine");

        // Assert
        results.Should().HaveCount(1);
    }

    // ── AbductiveReasoningAsync ─────────────────────────────────────

    [Fact]
    public async Task AbductiveReasoningAsync_ValidObservations_ReturnsHypothesis()
    {
        // Arrange
        _memoryMock.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("none"));

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("HYPOTHESIS: Memory leak causes crashes\nCONFIDENCE: 0.8\nDOMAIN: reliability\nEVIDENCE: Consistent pattern");

        var engine = CreateEngine();

        // Act
        var result = await engine.AbductiveReasoningAsync(new List<string>
        {
            "Application crashes after 4 hours",
            "Memory usage grows continuously",
            "Restart resolves the issue temporarily"
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Statement.Should().Contain("Memory leak");
        result.Value.Domain.Should().Be("reliability");
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private HypothesisEngine CreateEngine()
    {
        return new HypothesisEngine(
            _llmMock.Object,
            _orchestratorMock.Object,
            _memoryMock.Object,
            _ethicsMock.Object);
    }

    private static MetaAIHypothesis CreateTestHypothesis()
    {
        return new MetaAIHypothesis(
            Guid.NewGuid(),
            "Test hypothesis statement",
            "testing",
            0.5,
            new List<string> { "initial evidence" },
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
    }

    private static Experiment CreateTestExperiment()
    {
        return new Experiment(
            Guid.NewGuid(),
            CreateTestHypothesis(),
            "Test experiment",
            new List<MetaAIPlanStep>
            {
                new("Step 1", new Dictionary<string, object>(), "outcome", 0.8)
            },
            new Dictionary<string, object>
            {
                ["if_true"] = "expected positive",
                ["if_false"] = "expected negative"
            },
            DateTime.UtcNow);
    }
}
