#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

namespace Ouroboros.Tests.MetaAI.GlobalWorkspaceTheory;

[Trait("Category", "Unit")]
public sealed class EntropyDriveTests
{
    [Fact]
    public void EntropyCalculator_EmptyWorkspace_ReturnsZero()
    {
        var calc = new EntropyCalculator();

        double entropy = calc.CalculateEntropy(Array.Empty<WorkspaceChunk>());

        entropy.Should().Be(0.0);
    }

    [Fact]
    public void EntropyCalculator_SingleSubsystem_ReturnsZero()
    {
        var calc = new EntropyCalculator();
        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Chat"), DateTime.UtcNow, 0.5),
            new(new Candidate("b", 0.5, 0.5, 0.5, 0.5, "Chat"), DateTime.UtcNow, 0.5)
        };

        double entropy = calc.CalculateEntropy(chunks);

        entropy.Should().Be(0.0);
    }

    [Fact]
    public void EntropyCalculator_UniformDistribution_ReturnsOne()
    {
        var calc = new EntropyCalculator();
        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Chat"), DateTime.UtcNow, 0.5),
            new(new Candidate("b", 0.5, 0.5, 0.5, 0.5, "Memory"), DateTime.UtcNow, 0.5),
            new(new Candidate("c", 0.5, 0.5, 0.5, 0.5, "Vision"), DateTime.UtcNow, 0.5)
        };

        double entropy = calc.CalculateEntropy(chunks);

        entropy.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void EntropyCalculator_SkewedDistribution_IsBetweenZeroAndOne()
    {
        var calc = new EntropyCalculator();
        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Chat"), DateTime.UtcNow, 0.5),
            new(new Candidate("b", 0.5, 0.5, 0.5, 0.5, "Chat"), DateTime.UtcNow, 0.5),
            new(new Candidate("c", 0.5, 0.5, 0.5, 0.5, "Memory"), DateTime.UtcNow, 0.5)
        };

        double entropy = calc.CalculateEntropy(chunks);

        entropy.Should().BeInRange(0.0, 1.0);
        entropy.Should().BeLessThan(1.0);
        entropy.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void EntropyCalculator_NullInput_ReturnsZero()
    {
        var calc = new EntropyCalculator();

        double entropy = calc.CalculateEntropy(null!);

        entropy.Should().Be(0.0);
    }

    [Fact]
    public void EvaluateState_LowEntropy_ReturnsBored()
    {
        var drive = new IntrinsicDrive();

        DriveState state = drive.EvaluateState(0.15);

        state.Should().Be(DriveState.Bored);
    }

    [Fact]
    public void EvaluateState_AtBoredThreshold_ReturnsBored()
    {
        var drive = new IntrinsicDrive { BoredThreshold = 0.3 };

        DriveState state = drive.EvaluateState(0.29);

        state.Should().Be(DriveState.Bored);
    }

    [Fact]
    public void EvaluateState_HealthyMid_ReturnsHealthy()
    {
        var drive = new IntrinsicDrive();

        DriveState state = drive.EvaluateState(0.5);

        state.Should().Be(DriveState.Healthy);
    }

    [Fact]
    public void EvaluateState_AtOverwhelmedThreshold_ReturnsOverwhelmed()
    {
        var drive = new IntrinsicDrive { OverwhelmedThreshold = 0.7 };

        DriveState state = drive.EvaluateState(0.71);

        state.Should().Be(DriveState.Overwhelmed);
    }

    [Fact]
    public void EvaluateState_MaxEntropy_ReturnsOverwhelmed()
    {
        var drive = new IntrinsicDrive();

        DriveState state = drive.EvaluateState(1.0);

        state.Should().Be(DriveState.Overwhelmed);
    }

    [Fact]
    public void AdjustSalience_Bored_AddsExplorationBonus()
    {
        var drive = new IntrinsicDrive { ExplorationBonus = 0.15 };
        var candidate = new Candidate("x", 0.5, 0.5, 0.5, 0.5, "Src");

        double adjusted = drive.AdjustSalience(candidate, 0.5, DriveState.Bored);

        adjusted.Should().BeApproximately(0.65, 0.001);
    }

    [Fact]
    public void AdjustSalience_Bored_ClampsToOne()
    {
        var drive = new IntrinsicDrive { ExplorationBonus = 0.5 };
        var candidate = new Candidate("x", 0.8, 0.8, 0.8, 0.8, "Src");

        double adjusted = drive.AdjustSalience(candidate, 0.9, DriveState.Bored);

        adjusted.Should().Be(1.0);
    }

    [Fact]
    public void AdjustSalience_Healthy_ReturnsBase()
    {
        var drive = new IntrinsicDrive();
        var candidate = new Candidate("x", 0.5, 0.5, 0.5, 0.5, "Src");

        double adjusted = drive.AdjustSalience(candidate, 0.6, DriveState.Healthy);

        adjusted.Should().Be(0.6);
    }

    [Fact]
    public void AdjustSalience_Overwhelmed_AddsFocusBonusAndSuppressesNovelty()
    {
        var drive = new IntrinsicDrive { FocusBonus = 0.10, NoveltySuppression = 0.20 };
        var candidate = new Candidate("x", 0.5, 0.8, 0.5, 0.5, "Src");

        double adjusted = drive.AdjustSalience(candidate, 0.5, DriveState.Overwhelmed);

        double expected = 0.5 + 0.10 - (0.8 * 0.20);
        adjusted.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void AdjustSalience_Overwhelmed_NoNegativeResult()
    {
        var drive = new IntrinsicDrive { FocusBonus = 0.0, NoveltySuppression = 1.0 };
        var candidate = new Candidate("x", 0.0, 1.0, 0.0, 0.0, "Src");

        double adjusted = drive.AdjustSalience(candidate, 0.0, DriveState.Overwhelmed);

        adjusted.Should().Be(0.0);
    }

    [Fact]
    public void AdjustSalience_NullCandidate_ThrowsArgumentNullException()
    {
        var drive = new IntrinsicDrive();

        Action act = () => drive.AdjustSalience(null!, 0.5, DriveState.Healthy);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Influence_EmptyCandidates_ReturnsEmpty()
    {
        var influencer = new DriveInfluencer(
            new SalienceScorer(), new EntropyCalculator(), new IntrinsicDrive());

        IReadOnlyList<ScoredCandidate> result = influencer.Influence(Array.Empty<Candidate>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Influence_WithChunks_CalculatesEntropyFromWorkspace()
    {
        var influencer = new DriveInfluencer(
            new SalienceScorer(), new EntropyCalculator(), new IntrinsicDrive());
        var candidates = new List<Candidate>
        {
            new("a", 0.5, 0.5, 0.5, 0.5, "Chat")
        };
        var workspaceChunks = new List<WorkspaceChunk>
        {
            new(new Candidate("old", 0.5, 0.5, 0.5, 0.5, "Chat"), DateTime.UtcNow, 0.5),
            new(new Candidate("old2", 0.5, 0.5, 0.5, 0.5, "Memory"), DateTime.UtcNow, 0.5)
        };

        IReadOnlyList<ScoredCandidate> result = influencer.Influence(candidates, workspaceChunks);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Influence_BoredState_BoostsSalience()
    {
        var scorer = new SalienceScorer();
        var drive = new IntrinsicDrive { ExplorationBonus = 0.3 };
        var influencer = new DriveInfluencer(scorer, new EntropyCalculator(), drive);

        // Empty workspace = 0 entropy = bored state
        var candidates = new List<Candidate>
        {
            new("a", 0.5, 0.5, 0.5, 0.5, "Chat")
        };

        IReadOnlyList<ScoredCandidate> result = influencer.Influence(candidates);
        double baseSalience = scorer.CalculateSalience(candidates[0]);

        result[0].Salience.Should().BeGreaterThan(baseSalience);
    }

    [Fact]
    public void Influence_ReturnsOrderedByDescendingSalience()
    {
        var influencer = new DriveInfluencer(
            new SalienceScorer(), new EntropyCalculator(), new IntrinsicDrive());
        var candidates = new List<Candidate>
        {
            new("low", 0.1, 0.1, 0.1, 0.1, "A"),
            new("high", 0.9, 0.9, 0.9, 0.9, "B"),
            new("mid", 0.5, 0.5, 0.5, 0.5, "C")
        };

        IReadOnlyList<ScoredCandidate> result = influencer.Influence(candidates);

        result.Should().BeInDescendingOrder(r => r.Salience);
    }

    [Fact]
    public void Influence_NullCandidates_ThrowsArgumentNullException()
    {
        var influencer = new DriveInfluencer(
            new SalienceScorer(), new EntropyCalculator(), new IntrinsicDrive());

        Action act = () => influencer.Influence(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
