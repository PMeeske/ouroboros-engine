#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using FluentAssertions;
using Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;
using Xunit;

namespace Ouroboros.Tests.MetaAI.GlobalWorkspaceTheory;

[Trait("Category", "Unit")]
public sealed class GwtWorkspaceTests
{
    #region Construction

    [Fact]
    public void Constructor_DefaultCapacity_IsFive()
    {
        var workspace = new GwtWorkspace();

        workspace.Capacity.Should().Be(5);
        workspace.Chunks.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_CustomCapacity_RespectsValue()
    {
        var workspace = new GwtWorkspace(7);

        workspace.Capacity.Should().Be(7);
    }

    [Fact]
    public void Constructor_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new GwtWorkspace(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new GwtWorkspace(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region CompeteAndReplace

    [Fact]
    public void CompeteAndReplace_EmptyCandidates_NoChange()
    {
        var workspace = new GwtWorkspace();

        CompetitionResult result = workspace.CompeteAndReplace(Array.Empty<ScoredCandidate>());

        result.Admitted.Should().BeEmpty();
        result.Evicted.Should().BeEmpty();
        workspace.Chunks.Should().BeEmpty();
    }

    [Fact]
    public void CompeteAndReplace_UnderCapacity_AdmitAll()
    {
        var workspace = new GwtWorkspace(5);
        var candidates = new List<ScoredCandidate>
        {
            new(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), 0.5),
            new(new Candidate("b", 0.6, 0.6, 0.6, 0.6, "Src"), 0.6)
        };

        CompetitionResult result = workspace.CompeteAndReplace(candidates);

        result.Admitted.Should().HaveCount(2);
        result.Evicted.Should().BeEmpty();
        workspace.Chunks.Should().HaveCount(2);
    }

    [Fact]
    public void CompeteAndReplace_AtCapacity_LowestSalienceEvicted()
    {
        var workspace = new GwtWorkspace(2);
        var initial = new List<ScoredCandidate>
        {
            new(new Candidate("low", 0.1, 0.1, 0.1, 0.1, "Src"), 0.1),
            new(new Candidate("high", 0.9, 0.9, 0.9, 0.9, "Src"), 0.9)
        };
        workspace.CompeteAndReplace(initial);

        var incoming = new List<ScoredCandidate>
        {
            new(new Candidate("higher", 0.95, 0.95, 0.95, 0.95, "Src"), 0.95)
        };
        CompetitionResult result = workspace.CompeteAndReplace(incoming);

        result.Admitted.Should().HaveCount(1);
        result.Evicted.Should().HaveCount(1);
        result.Evicted[0].Candidate.Content.Should().Be("low");
        workspace.Chunks.Should().HaveCount(2);
    }

    [Fact]
    public void CompeteAndReplace_WhenFull_StrongerCandidateReplacesWeaker()
    {
        var workspace = new GwtWorkspace(1);
        workspace.CompeteAndReplace(new[] { new ScoredCandidate(new Candidate("weak", 0.2, 0.2, 0.2, 0.2, "A"), 0.2) });

        CompetitionResult result = workspace.CompeteAndReplace(
            new[] { new ScoredCandidate(new Candidate("strong", 0.8, 0.8, 0.8, 0.8, "A"), 0.8) });

        result.Admitted.Should().HaveCount(1);
        result.Evicted.Should().HaveCount(1);
        workspace.Chunks.Should().HaveCount(1);
        workspace.Chunks[0].Candidate.Content.Should().Be("strong");
    }

    [Fact]
    public void CompeteAndReplace_WhenFull_WeakCandidateDoesNotReplaceStrong()
    {
        var workspace = new GwtWorkspace(1);
        workspace.CompeteAndReplace(new[] { new ScoredCandidate(new Candidate("strong", 0.8, 0.8, 0.8, 0.8, "A"), 0.8) });

        CompetitionResult result = workspace.CompeteAndReplace(
            new[] { new ScoredCandidate(new Candidate("weak", 0.2, 0.2, 0.2, 0.2, "A"), 0.2) });

        result.Admitted.Should().BeEmpty();
        result.Evicted.Should().BeEmpty();
        workspace.Chunks[0].Candidate.Content.Should().Be("strong");
    }

    [Fact]
    public void CompeteAndReplace_DuplicateCandidate_Skipped()
    {
        var workspace = new GwtWorkspace(2);
        var candidate = new Candidate("same", 0.5, 0.5, 0.5, 0.5, "Src");
        workspace.CompeteAndReplace(new[] { new ScoredCandidate(candidate, 0.5) });

        CompetitionResult result = workspace.CompeteAndReplace(new[] { new ScoredCandidate(candidate, 0.5) });

        result.Admitted.Should().BeEmpty();
        result.Evicted.Should().BeEmpty();
        workspace.Chunks.Should().HaveCount(1);
    }

    [Fact]
    public void CompeteAndReplace_NullCandidates_ThrowsArgumentNullException()
    {
        var workspace = new GwtWorkspace();

        Action act = () => workspace.CompeteAndReplace(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CompeteAndReplace_MultipleOrderedBySalience_CorrectWinners()
    {
        var workspace = new GwtWorkspace(3);
        var candidates = Enumerable.Range(1, 10)
            .Select(i => new ScoredCandidate(
                new Candidate($"c{i}", i / 10.0, i / 10.0, i / 10.0, i / 10.0, "Src"),
                i / 10.0))
            .ToList();

        CompetitionResult result = workspace.CompeteAndReplace(candidates);

        result.Admitted.Should().HaveCount(3);
        workspace.Chunks.Select(c => c.Candidate.Content)
            .Should().BeEquivalentTo(new[] { "c10", "c9", "c8" }, options => options.WithStrictOrdering());
    }

    #endregion

    #region RemoveChunk / Clear

    [Fact]
    public void RemoveChunk_Existing_ReturnsTrue()
    {
        var workspace = new GwtWorkspace();
        var candidate = new Candidate("x", 0.5, 0.5, 0.5, 0.5, "Src");
        workspace.CompeteAndReplace(new[] { new ScoredCandidate(candidate, 0.5) });

        bool removed = workspace.RemoveChunk(candidate.Id);

        removed.Should().BeTrue();
        workspace.Chunks.Should().BeEmpty();
    }

    [Fact]
    public void RemoveChunk_Missing_ReturnsFalse()
    {
        var workspace = new GwtWorkspace();

        bool removed = workspace.RemoveChunk(Guid.NewGuid());

        removed.Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var workspace = new GwtWorkspace();
        workspace.CompeteAndReplace(new[]
        {
            new ScoredCandidate(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), 0.5),
            new ScoredCandidate(new Candidate("b", 0.6, 0.6, 0.6, 0.6, "Src"), 0.6)
        });

        workspace.Clear();

        workspace.Chunks.Should().BeEmpty();
    }

    #endregion

    #region GetSnapshot

    [Fact]
    public void GetSnapshot_EmptyWorkspace_HasCorrectProperties()
    {
        var workspace = new GwtWorkspace(5);

        WorkspaceSnapshot snapshot = workspace.GetSnapshot();

        snapshot.Capacity.Should().Be(5);
        snapshot.Count.Should().Be(0);
        snapshot.FreeSlots.Should().Be(5);
        snapshot.IsFull.Should().BeFalse();
        snapshot.Chunks.Should().BeEmpty();
        snapshot.CapturedAt.Should().BeWithin(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetSnapshot_FullWorkspace_IsFullTrue()
    {
        var workspace = new GwtWorkspace(2);
        workspace.CompeteAndReplace(new[]
        {
            new ScoredCandidate(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), 0.5),
            new ScoredCandidate(new Candidate("b", 0.6, 0.6, 0.6, 0.6, "Src"), 0.6)
        });

        WorkspaceSnapshot snapshot = workspace.GetSnapshot();

        snapshot.IsFull.Should().BeTrue();
        snapshot.FreeSlots.Should().Be(0);
        snapshot.Count.Should().Be(2);
    }

    [Fact]
    public void GetSnapshot_IsImmutable()
    {
        var workspace = new GwtWorkspace();
        workspace.CompeteAndReplace(new[]
        {
            new ScoredCandidate(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), 0.5)
        });

        WorkspaceSnapshot snapshot = workspace.GetSnapshot();
        workspace.Clear();

        snapshot.Chunks.Should().HaveCount(1);
    }

    #endregion

    #region WorkspaceChunk / WorkspaceSnapshot Records

    [Fact]
    public void WorkspaceChunk_RecordEquality_SameValues_AreEqual()
    {
        var candidate = new Candidate("x", 0.5, 0.5, 0.5, 0.5, "Src");
        var now = DateTime.UtcNow;
        var a = new WorkspaceChunk(candidate, now, 0.5);
        var b = new WorkspaceChunk(candidate, now, 0.5);

        a.Should().Be(b);
    }

    [Fact]
    public void WorkspaceSnapshot_RecordProperties_Correct()
    {
        var chunks = new List<WorkspaceChunk>
        {
            new(new Candidate("a", 0.5, 0.5, 0.5, 0.5, "Src"), DateTime.UtcNow, 0.5)
        };
        var snapshot = new WorkspaceSnapshot(chunks, 5, DateTime.UtcNow);

        snapshot.Count.Should().Be(1);
        snapshot.FreeSlots.Should().Be(4);
        snapshot.IsFull.Should().BeFalse();
    }

    #endregion
}
