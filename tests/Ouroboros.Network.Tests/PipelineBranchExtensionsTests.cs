using LangChain.DocumentLoaders;
using Ouroboros.Domain.Vectors;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class PipelineBranchExtensionsTests
{
    private static PipelineBranch CreateBranch(string name = "test")
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    [Fact]
    public void ToMerkleDag_NullBranch_ReturnsFailure()
    {
        PipelineBranch? nullBranch = null;

        var result = nullBranch!.ToMerkleDag();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ToMerkleDag_EmptyBranch_ReturnsSuccess()
    {
        var branch = CreateBranch();

        var result = branch.ToMerkleDag();

        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(0);
    }

    [Fact]
    public void CreateReifier_ReturnsFunctionalReifier()
    {
        var branch = CreateBranch();

        var reifier = branch.CreateReifier();

        reifier.Should().NotBeNull();
        reifier.Dag.Should().NotBeNull();
    }

    [Fact]
    public void GetLatestReasoningNode_NoReasoningSteps_ReturnsNone()
    {
        var branch = CreateBranch();

        var result = branch.GetLatestReasoningNode();

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetReasoningSummary_EmptyBranch_ReturnsZeroCounts()
    {
        var branch = CreateBranch();

        var summary = branch.GetReasoningSummary();

        summary.BranchName.Should().Be("test");
        summary.TotalSteps.Should().Be(0);
        summary.TotalToolCalls.Should().Be(0);
        summary.TotalDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ProjectNetworkState_EmptyBranch_ReturnsSuccess()
    {
        var branch = CreateBranch();

        var result = branch.ProjectNetworkState();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalNodes.Should().Be(0);
    }
}
