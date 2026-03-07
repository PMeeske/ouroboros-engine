namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class PipelineBranchReifierTests
{
    [Fact]
    public void Ctor_Parameterless_InitializesCorrectly()
    {
        var reifier = new PipelineBranchReifier();

        reifier.Dag.Should().NotBeNull();
        reifier.Projector.Should().NotBeNull();
        reifier.EventToNodeMapping.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_WithDagAndProjector_InitializesCorrectly()
    {
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        var reifier = new PipelineBranchReifier(dag, projector);

        reifier.Dag.Should().BeSameAs(dag);
        reifier.Projector.Should().BeSameAs(projector);
        reifier.EventToNodeMapping.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_NullDag_Throws()
    {
        var projector = new NetworkStateProjector(new MerkleDag());

        FluentActions.Invoking(() => new PipelineBranchReifier(null!, projector))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullProjector_Throws()
    {
        var dag = new MerkleDag();

        FluentActions.Invoking(() => new PipelineBranchReifier(dag, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReifyBranch_Null_ReturnsFailure()
    {
        var reifier = new PipelineBranchReifier();

        var result = reifier.ReifyBranch(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReifyEvent_Null_ReturnsFailure()
    {
        var reifier = new PipelineBranchReifier();

        var result = reifier.ReifyEvent(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReifyNewEvents_Null_ReturnsFailure()
    {
        var reifier = new PipelineBranchReifier();

        var result = reifier.ReifyNewEvents(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateSnapshot_EmptyDag_ReturnsValidState()
    {
        var reifier = new PipelineBranchReifier();

        var snapshot = reifier.CreateSnapshot("test-branch");

        snapshot.Should().NotBeNull();
        snapshot.TotalNodes.Should().Be(0);
    }

    [Fact]
    public void CreateSnapshot_NullBranchName_ReturnsValidState()
    {
        var reifier = new PipelineBranchReifier();

        var snapshot = reifier.CreateSnapshot();

        snapshot.Should().NotBeNull();
    }
}
