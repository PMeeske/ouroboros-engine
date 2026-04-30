#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

namespace Ouroboros.Tests.MetaAI.GlobalWorkspaceTheory;

[Trait("Category", "Unit")]
public sealed class CompetitionEngineTests
{
    [Fact]
    public void SalienceScorer_StandardWeights_CalculatesCorrectly()
    {
        var scorer = new SalienceScorer();
        var candidate = new Candidate("test", 1.0, 1.0, 1.0, 1.0, "Chat");

        double salience = scorer.CalculateSalience(candidate);

        salience.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void SalienceScorer_ZeroValues_ReturnsZero()
    {
        var scorer = new SalienceScorer();
        var candidate = new Candidate("test", 0.0, 0.0, 0.0, 0.0, "Chat");

        double salience = scorer.CalculateSalience(candidate);

        salience.Should().Be(0.0);
    }

    [Fact]
    public void SalienceScorer_WeightedSum_IsCorrect()
    {
        var scorer = new SalienceScorer();
        var candidate = new Candidate("test", 0.5, 0.4, 0.6, 0.3, "Chat");

        double salience = scorer.CalculateSalience(candidate);

        double expected = (0.5 * 0.3) + (0.4 * 0.25) + (0.6 * 0.25) + (0.3 * 0.2);
        salience.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void SalienceScorer_ExcessValues_AreClamped()
    {
        var scorer = new SalienceScorer();
        var candidate = new Candidate("test", 2.0, 2.0, 2.0, 2.0, "Chat");

        double salience = scorer.CalculateSalience(candidate);

        salience.Should().Be(1.0);
    }

    [Fact]
    public void SalienceScorer_NegativeValues_AreClamped()
    {
        var scorer = new SalienceScorer();
        var candidate = new Candidate("test", -1.0, -1.0, -1.0, -1.0, "Chat");

        double salience = scorer.CalculateSalience(candidate);

        salience.Should().Be(0.0);
    }

    [Fact]
    public void SalienceScorer_CustomWeights_RespectsWeights()
    {
        var scorer = new SalienceScorer { UrgencyWeight = 1.0, NoveltyWeight = 0.0, RelevanceWeight = 0.0, ConfidenceWeight = 0.0 };
        var candidate = new Candidate("test", 0.7, 0.2, 0.3, 0.4, "Chat");

        double salience = scorer.CalculateSalience(candidate);

        salience.Should().BeApproximately(0.7, 0.001);
    }

    [Fact]
    public void SalienceScorer_NullCandidate_ThrowsArgumentNullException()
    {
        var scorer = new SalienceScorer();

        Action act = () => scorer.CalculateSalience(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Compete_EmptyCandidates_ReturnsEmpty()
    {
        var engine = new CompetitionEngine();

        IReadOnlyList<ScoredCandidate> winners = engine.Compete(Array.Empty<Candidate>(), 3);

        winners.Should().BeEmpty();
    }

    [Fact]
    public void Compete_SelectsTopN_BySalience()
    {
        var engine = new CompetitionEngine();
        var candidates = new List<Candidate>
        {
            new("low", 0.1, 0.1, 0.1, 0.1, "A"),
            new("high", 1.0, 1.0, 1.0, 1.0, "B"),
            new("mid", 0.5, 0.5, 0.5, 0.5, "C")
        };

        IReadOnlyList<ScoredCandidate> winners = engine.Compete(candidates, 2);

        winners.Should().HaveCount(2);
        winners[0].Candidate.Content.Should().Be("high");
        winners[1].Candidate.Content.Should().Be("mid");
    }

    [Fact]
    public void Compete_TopNGreaterThanCount_ReturnsAll()
    {
        var engine = new CompetitionEngine();
        var candidates = new List<Candidate>
        {
            new("a", 0.5, 0.5, 0.5, 0.5, "A")
        };

        IReadOnlyList<ScoredCandidate> winners = engine.Compete(candidates, 5);

        winners.Should().HaveCount(1);
    }

    [Fact]
    public void Compete_ReturnsOrderedByDescendingSalience()
    {
        var engine = new CompetitionEngine();
        var candidates = Enumerable.Range(1, 10)
            .Select(i => new Candidate($"c{i}", i / 10.0, i / 10.0, i / 10.0, i / 10.0, "Src"))
            .ToList();

        IReadOnlyList<ScoredCandidate> winners = engine.Compete(candidates, 5);

        winners.Should().BeInDescendingOrder(w => w.Salience);
    }

    [Fact]
    public void Compete_NullCandidates_ThrowsArgumentNullException()
    {
        var engine = new CompetitionEngine();

        Action act = () => engine.Compete(null!, 3);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Compete_ZeroTopN_ThrowsArgumentOutOfRangeException()
    {
        var engine = new CompetitionEngine();

        Action act = () => engine.Compete(new List<Candidate>(), 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Compete_NegativeTopN_ThrowsArgumentOutOfRangeException()
    {
        var engine = new CompetitionEngine();

        Action act = () => engine.Compete(new List<Candidate>(), -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CompeteAsync_WithMockedProducers_GathersAndSelects()
    {
        var engine = new CompetitionEngine();

        var producerA = new Mock<ICandidateProducer>();
        producerA.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate> { new("a1", 0.9, 0.9, 0.9, 0.9, "A") });
        producerA.Setup(p => p.SubsystemName).Returns("A");

        var producerB = new Mock<ICandidateProducer>();
        producerB.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate> { new("b1", 0.5, 0.5, 0.5, 0.5, "B") });
        producerB.Setup(p => p.SubsystemName).Returns("B");

        IReadOnlyList<ScoredCandidate> winners = await engine.CompeteAsync(
            new[] { producerA.Object, producerB.Object }, 2);

        winners.Should().HaveCount(2);
        winners[0].Candidate.Content.Should().Be("a1");
        winners[1].Candidate.Content.Should().Be("b1");
    }

    [Fact]
    public async Task CompeteAsync_NoProducers_ReturnsEmpty()
    {
        var engine = new CompetitionEngine();

        IReadOnlyList<ScoredCandidate> winners = await engine.CompeteAsync(Array.Empty<ICandidateProducer>(), 3);

        winners.Should().BeEmpty();
    }

    [Fact]
    public async Task CompeteAsync_ProducerThrows_PropagatesException()
    {
        var engine = new CompetitionEngine();

        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        Func<Task> act = async () => await engine.CompeteAsync(new[] { producer.Object }, 1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CompeteAsync_RespectsCancellation()
    {
        var engine = new CompetitionEngine();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var producer = new Mock<ICandidateProducer>();
        producer.Setup(p => p.ProduceCandidatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Candidate>());

        IReadOnlyList<ScoredCandidate> winners = await engine.CompeteAsync(
            new[] { producer.Object }, 1, cts.Token);

        winners.Should().BeEmpty();
    }

    [Fact]
    public async Task CompeteAsync_NullProducers_ThrowsArgumentNullException()
    {
        var engine = new CompetitionEngine();

        Func<Task> act = async () => await engine.CompeteAsync(null!, 1);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Candidate_RecordEquality_SameValues_AreEqual()
    {
        var a = new Candidate(Guid.NewGuid(), "content", 0.5, 0.5, 0.5, 0.5, "Src");
        var b = a with { };

        a.Should().Be(b);
    }

    [Fact]
    public void Candidate_RecordEquality_DifferentIds_AreNotEqual()
    {
        var a = new Candidate(Guid.NewGuid(), "content", 0.5, 0.5, 0.5, 0.5, "Src");
        var b = a with { Id = Guid.NewGuid() };

        a.Should().NotBe(b);
    }

    [Fact]
    public void Candidate_ConvenienceCtor_GeneratesGuid()
    {
        var c = new Candidate("content", 0.5, 0.5, 0.5, 0.5, "Src");

        c.Id.Should().NotBe(Guid.Empty);
    }
}
