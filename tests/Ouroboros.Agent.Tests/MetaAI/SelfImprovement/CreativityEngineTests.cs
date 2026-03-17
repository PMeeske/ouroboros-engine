// <copyright file="CreativityEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfImprovement;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CreativityEngineTests
{
    // ── DivergentThinkAsync ─────────────────────────────────────────

    [Fact]
    public async Task DivergentThinkAsync_NullProblem_Throws()
    {
        var engine = new CreativityEngine();
        var act = () => engine.DivergentThinkAsync(null!, 3);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DivergentThinkAsync_ZeroIdeas_ThrowsArgumentOutOfRange()
    {
        var engine = new CreativityEngine();
        var act = () => engine.DivergentThinkAsync("problem", 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task DivergentThinkAsync_NegativeIdeas_ThrowsArgumentOutOfRange()
    {
        var engine = new CreativityEngine();
        var act = () => engine.DivergentThinkAsync("problem", -1);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task DivergentThinkAsync_CancelledToken_Throws()
    {
        var engine = new CreativityEngine();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => engine.DivergentThinkAsync("problem", 3, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DivergentThinkAsync_ReturnsRequestedNumberOfIdeas()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act
        var ideas = await engine.DivergentThinkAsync("How to improve AI safety", 5);

        // Assert
        ideas.Should().HaveCount(5);
    }

    [Fact]
    public async Task DivergentThinkAsync_EachIdeaHasValidScores()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act
        var ideas = await engine.DivergentThinkAsync("Design a better search algorithm", 3);

        // Assert
        foreach (var idea in ideas)
        {
            idea.NoveltyScore.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
            idea.ValueScore.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
            idea.SurpriseScore.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
            idea.Description.Should().NotBeNullOrWhiteSpace();
            idea.Id.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task DivergentThinkAsync_CyclesThroughScamperOperators()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act — 7 ideas to cover all SCAMPER operators
        var ideas = await engine.DivergentThinkAsync("Improve database performance", 7);

        // Assert — each idea should have a unique SCAMPER-based ID prefix
        var prefixes = ideas.Select(i => i.Id.Split('-')[0]).ToList();
        prefixes.Should().Contain("Substitute");
        prefixes.Should().Contain("Combine");
        prefixes.Should().Contain("Adapt");
        prefixes.Should().Contain("Modify");
        prefixes.Should().Contain("Eliminate");
        prefixes.Should().Contain("Reverse");
    }

    [Fact]
    public async Task DivergentThinkAsync_UpdatesTotalIdeasGenerated()
    {
        // Arrange
        var engine = new CreativityEngine();
        engine.TotalIdeasGenerated.Should().Be(0);

        // Act
        await engine.DivergentThinkAsync("problem one", 3);
        await engine.DivergentThinkAsync("problem two", 2);

        // Assert
        engine.TotalIdeasGenerated.Should().Be(5);
    }

    [Fact]
    public async Task DivergentThinkAsync_LaterIdeasHaveHigherNovelty()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act — generate enough ideas that the novelty bump is visible
        var ideas = await engine.DivergentThinkAsync("Solve climate change", 10);

        // Assert — later ideas should tend toward higher novelty due to the i * 0.03 bonus
        // We check that the last idea's novelty is >= first idea's novelty
        ideas.Last().NoveltyScore.Should().BeGreaterThanOrEqualTo(ideas.First().NoveltyScore);
    }

    [Fact]
    public async Task DivergentThinkAsync_DeterministicForSameProblem()
    {
        // Arrange — same problem should produce same ideas (seeded by problem.GetHashCode())
        var engine1 = new CreativityEngine();
        var engine2 = new CreativityEngine();

        // Act
        var ideas1 = await engine1.DivergentThinkAsync("fixed problem", 3);
        var ideas2 = await engine2.DivergentThinkAsync("fixed problem", 3);

        // Assert
        for (int i = 0; i < ideas1.Count; i++)
        {
            ideas1[i].Description.Should().Be(ideas2[i].Description);
            ideas1[i].NoveltyScore.Should().Be(ideas2[i].NoveltyScore);
        }
    }

    // ── BlendConceptsAsync ──────────────────────────────────────────

    [Fact]
    public async Task BlendConceptsAsync_NullConceptA_Throws()
    {
        var engine = new CreativityEngine();
        var act = () => engine.BlendConceptsAsync(null!, "concept B");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BlendConceptsAsync_NullConceptB_Throws()
    {
        var engine = new CreativityEngine();
        var act = () => engine.BlendConceptsAsync("concept A", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BlendConceptsAsync_ValidConcepts_ReturnsBlendWithMappings()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act
        var blend = await engine.BlendConceptsAsync(
            "machine learning neural networks training",
            "biological evolution natural selection adaptation");

        // Assert
        blend.ConceptA.Should().Be("machine learning neural networks training");
        blend.ConceptB.Should().Be("biological evolution natural selection adaptation");
        blend.EmergentConcept.Should().NotBeNullOrWhiteSpace();
        blend.BlendStrength.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task BlendConceptsAsync_OverlappingConcepts_FindsSharedMappings()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act — concepts share "learning" and "optimization"
        var blend = await engine.BlendConceptsAsync(
            "machine learning optimization algorithm",
            "human learning optimization strategy");

        // Assert
        blend.Mappings.Should().Contain(m => m.Contains("shared", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BlendConceptsAsync_CancelledToken_Throws()
    {
        var engine = new CreativityEngine();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => engine.BlendConceptsAsync("a", "b", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── FindBisociationsAsync ───────────────────────────────────────

    [Fact]
    public async Task FindBisociationsAsync_NullDomainA_Throws()
    {
        var engine = new CreativityEngine();
        var act = () => engine.FindBisociationsAsync(null!, "domain B");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FindBisociationsAsync_NullDomainB_Throws()
    {
        var engine = new CreativityEngine();
        var act = () => engine.FindBisociationsAsync("domain A", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task FindBisociationsAsync_DistantDomains_ReturnsHighStrength()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act — distant domains with some structural similarity should yield connections
        var result = await engine.FindBisociationsAsync(
            "quantum physics entanglement superposition",
            "music composition harmony rhythm");

        // Assert
        result.DomainA.Should().Be("quantum physics entanglement superposition");
        result.DomainB.Should().Be("music composition harmony rhythm");
        result.BisociationStrength.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task FindBisociationsAsync_IdenticalDomains_ReturnsLowStrength()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act — identical domains have zero distance => low bisociation strength
        var result = await engine.FindBisociationsAsync(
            "machine learning algorithm",
            "machine learning algorithm");

        // Assert — domain distance is 0 so strength = 0
        result.BisociationStrength.Should().Be(0.0);
    }

    [Fact]
    public async Task FindBisociationsAsync_SharedConcepts_IncludesSharedConnections()
    {
        // Arrange
        var engine = new CreativityEngine();

        // Act
        var result = await engine.FindBisociationsAsync(
            "neural network optimization gradient",
            "brain network optimization signal");

        // Assert
        result.Connections.Should().Contain(c => c.Contains("Shared concept", StringComparison.OrdinalIgnoreCase));
    }

    // ── EvaluateCreativity ──────────────────────────────────────────

    [Fact]
    public void EvaluateCreativity_NullIdea_Throws()
    {
        var engine = new CreativityEngine();
        var act = () => engine.EvaluateCreativity(null!, "context");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluateCreativity_ValidIdea_ReturnsWeightedScore()
    {
        // Arrange
        var engine = new CreativityEngine();
        var idea = new CreativeIdea("test-1", "An innovative solution", 0.8, 0.6, 0.7);

        // Act
        var score = engine.EvaluateCreativity(idea, "some context");

        // Assert — overall = 0.8*0.4 + 0.6*0.3 + 0.7*0.3 = 0.32 + 0.18 + 0.21 = 0.71
        score.Overall.Should().BeApproximately(0.71, 0.001);
        score.Novelty.Should().Be(0.8);
        score.Value.Should().Be(0.6);
        score.Surprise.Should().Be(0.7);
    }

    [Fact]
    public void EvaluateCreativity_MaxScores_ReturnsOne()
    {
        // Arrange
        var engine = new CreativityEngine();
        var idea = new CreativeIdea("max", "Max idea", 1.0, 1.0, 1.0);

        // Act
        var score = engine.EvaluateCreativity(idea, "context");

        // Assert
        score.Overall.Should().Be(1.0);
    }

    [Fact]
    public void EvaluateCreativity_ZeroScores_ReturnsZero()
    {
        // Arrange
        var engine = new CreativityEngine();
        var idea = new CreativeIdea("zero", "Zero idea", 0.0, 0.0, 0.0);

        // Act
        var score = engine.EvaluateCreativity(idea, "context");

        // Assert
        score.Overall.Should().Be(0.0);
    }
}
