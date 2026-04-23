using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.SelfModel;

[Trait("Category", "Unit")]
public class SelfModelRecordsTests
{
    #region AgentCommitment

    [Fact]
    public void AgentCommitment_Creation_ShouldSetProperties()
    {
        var deps = new List<string> { "dep1" };
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var created = DateTime.UtcNow;
        var deadline = created.AddDays(7);

        var commitment = new AgentCommitment(Guid.NewGuid(), "desc", deadline, 0.8, CommitmentStatus.InProgress, 50.0, deps, metadata, created, null);

        commitment.Description.Should().Be("desc");
        commitment.Deadline.Should().Be(deadline);
        commitment.Priority.Should().Be(0.8);
        commitment.Status.Should().Be(CommitmentStatus.InProgress);
        commitment.ProgressPercent.Should().Be(50.0);
        commitment.Dependencies.Should().BeEquivalentTo(deps);
        commitment.Metadata.Should().BeEquivalentTo(metadata);
        commitment.CreatedAt.Should().Be(created);
        commitment.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void AgentCommitment_Creation_WithCompletedAt_ShouldSetProperties()
    {
        var completedAt = DateTime.UtcNow;
        var commitment = new AgentCommitment(Guid.NewGuid(), "desc", DateTime.UtcNow, 0.8, CommitmentStatus.Completed, 100.0, new List<string>(), new Dictionary<string, object>(), DateTime.UtcNow, completedAt);

        commitment.CompletedAt.Should().Be(completedAt);
    }

    #endregion

    #region AgentIdentityState

    [Fact]
    public void AgentIdentityState_Creation_ShouldSetProperties()
    {
        var capabilities = new List<AgentCapability>();
        var resources = new List<AgentResource>();
        var commitments = new List<AgentCommitment>();
        var performance = new AgentPerformance(0.9, 100.0, 10, 9, 1, new Dictionary<string, double>(), new Dictionary<string, double>(), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var metadata = new Dictionary<string, object> { ["key"] = "val" };
        var timestamp = DateTime.UtcNow;

        var state = new AgentIdentityState(Guid.NewGuid(), "Agent", capabilities, resources, commitments, performance, timestamp, metadata);

        state.Name.Should().Be("Agent");
        state.Capabilities.Should().BeEquivalentTo(capabilities);
        state.Resources.Should().BeEquivalentTo(resources);
        state.Commitments.Should().BeEquivalentTo(commitments);
        state.Performance.Should().Be(performance);
        state.StateTimestamp.Should().Be(timestamp);
        state.Metadata.Should().BeEquivalentTo(metadata);
    }

    #endregion

    #region AgentPerformance

    [Fact]
    public void AgentPerformance_Creation_ShouldSetProperties()
    {
        var capRates = new Dictionary<string, double> { ["coding"] = 0.9 };
        var resUtil = new Dictionary<string, double> { ["cpu"] = 0.5 };
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var perf = new AgentPerformance(0.85, 150.0, 100, 85, 15, capRates, resUtil, start, end);

        perf.OverallSuccessRate.Should().Be(0.85);
        perf.AverageResponseTime.Should().Be(150.0);
        perf.TotalTasks.Should().Be(100);
        perf.SuccessfulTasks.Should().Be(85);
        perf.FailedTasks.Should().Be(15);
        perf.CapabilitySuccessRates.Should().BeEquivalentTo(capRates);
        perf.ResourceUtilization.Should().BeEquivalentTo(resUtil);
        perf.MeasurementPeriodStart.Should().Be(start);
        perf.MeasurementPeriodEnd.Should().Be(end);
    }

    #endregion

    #region AgentResource

    [Fact]
    public void AgentResource_Creation_ShouldSetProperties()
    {
        var metadata = new Dictionary<string, object> { ["region"] = "us-east" };
        var updated = DateTime.UtcNow;
        var resource = new AgentResource("CPU", "compute", 0.6, 1.0, "cores", updated, metadata);

        resource.Name.Should().Be("CPU");
        resource.Type.Should().Be("compute");
        resource.Available.Should().Be(0.6);
        resource.Total.Should().Be(1.0);
        resource.Unit.Should().Be("cores");
        resource.LastUpdated.Should().Be(updated);
        resource.Metadata.Should().BeEquivalentTo(metadata);
    }

    #endregion

    #region AttentionPolicy

    [Fact]
    public void AttentionPolicy_Creation_ShouldSetProperties()
    {
        var policy = new AttentionPolicy(100, 20, TimeSpan.FromHours(1), 0.3);

        policy.MaxWorkspaceSize.Should().Be(100);
        policy.MaxHighPriorityItems.Should().Be(20);
        policy.DefaultItemLifetime.Should().Be(TimeSpan.FromHours(1));
        policy.MinAttentionThreshold.Should().Be(0.3);
    }

    #endregion

    #region CommitmentStatus

    [Theory]
    [InlineData(CommitmentStatus.Planned)]
    [InlineData(CommitmentStatus.InProgress)]
    [InlineData(CommitmentStatus.Completed)]
    [InlineData(CommitmentStatus.Failed)]
    [InlineData(CommitmentStatus.Cancelled)]
    [InlineData(CommitmentStatus.AtRisk)]
    public void CommitmentStatus_AllValues_ShouldBeDefined(CommitmentStatus status)
    {
        ((int)status).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region GlobalWorkspace

    [Fact]
    public void GlobalWorkspace_Constructor_DefaultPolicy_ShouldInitialize()
    {
        var workspace = new GlobalWorkspace();
        workspace.Should().NotBeNull();
    }

    [Fact]
    public void GlobalWorkspace_Constructor_CustomPolicy_ShouldInitialize()
    {
        var policy = new AttentionPolicy(50, 10, TimeSpan.FromMinutes(30), 0.5);
        var workspace = new GlobalWorkspace(policy);
        workspace.Should().NotBeNull();
    }

    [Fact]
    public void GlobalWorkspace_AddItem_ShouldReturnItem()
    {
        var workspace = new GlobalWorkspace();
        var item = workspace.AddItem("content", WorkspacePriority.Normal, "source");

        item.Content.Should().Be("content");
        item.Priority.Should().Be(WorkspacePriority.Normal);
        item.Source.Should().Be("source");
    }

    [Fact]
    public void GlobalWorkspace_AddItem_NullContent_ShouldThrow()
    {
        var workspace = new GlobalWorkspace();
        Action act = () => workspace.AddItem(null!, WorkspacePriority.Normal, "source");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GlobalWorkspace_AddItem_NullSource_ShouldThrow()
    {
        var workspace = new GlobalWorkspace();
        Action act = () => workspace.AddItem("content", WorkspacePriority.Normal, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GlobalWorkspace_GetItems_ShouldReturnItems()
    {
        var workspace = new GlobalWorkspace();
        workspace.AddItem("content1", WorkspacePriority.Normal, "source1");
        workspace.AddItem("content2", WorkspacePriority.High, "source2");

        var items = workspace.GetItems();
        items.Should().HaveCount(2);
    }

    [Fact]
    public void GlobalWorkspace_GetStatistics_ShouldReturnStats()
    {
        var workspace = new GlobalWorkspace();
        workspace.AddItem("content", WorkspacePriority.Normal, "source");

        var stats = workspace.GetStatistics();
        stats.TotalItems.Should().Be(1);
    }

    [Fact]
    public void GlobalWorkspace_RemoveItem_ShouldRemove()
    {
        var workspace = new GlobalWorkspace();
        var item = workspace.AddItem("content", WorkspacePriority.Normal, "source");
        workspace.RemoveItem(item.Id);

        var items = workspace.GetItems();
        items.Should().BeEmpty();
    }

    #endregion

    #region IdentityGraph

    [Fact]
    public void IdentityGraph_Constructor_ValidArgs_ShouldInitialize()
    {
        var mockRegistry = new Mock<ICapabilityRegistry>();
        var graph = new IdentityGraph(Guid.NewGuid(), "Agent", mockRegistry.Object);

        graph.Should().NotBeNull();
    }

    [Fact]
    public void IdentityGraph_Constructor_NullName_ShouldThrow()
    {
        var mockRegistry = new Mock<ICapabilityRegistry>();
        Action act = () => new IdentityGraph(Guid.NewGuid(), null!, mockRegistry.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("agentName");
    }

    [Fact]
    public void IdentityGraph_Constructor_NullRegistry_ShouldThrow()
    {
        Action act = () => new IdentityGraph(Guid.NewGuid(), "Agent", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capabilityRegistry");
    }

    [Fact]
    public async Task IdentityGraph_GetStateAsync_ShouldReturnState()
    {
        var mockRegistry = new Mock<ICapabilityRegistry>();
        mockRegistry.Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentCapability>());

        var graph = new IdentityGraph(Guid.NewGuid(), "Agent", mockRegistry.Object);
        var state = await graph.GetStateAsync();

        state.Should().NotBeNull();
        state.Name.Should().Be("Agent");
    }

    [Fact]
    public void IdentityGraph_AddResource_ShouldAdd()
    {
        var mockRegistry = new Mock<ICapabilityRegistry>();
        var graph = new IdentityGraph(Guid.NewGuid(), "Agent", mockRegistry.Object);
        var resource = new AgentResource("CPU", "compute", 0.8, 1.0, "cores", DateTime.UtcNow, new Dictionary<string, object>());

        graph.AddResource(resource);
    }

    [Fact]
    public void IdentityGraph_AddCommitment_ShouldAdd()
    {
        var mockRegistry = new Mock<ICapabilityRegistry>();
        var graph = new IdentityGraph(Guid.NewGuid(), "Agent", mockRegistry.Object);
        var commitment = new AgentCommitment(Guid.NewGuid(), "desc", DateTime.UtcNow.AddDays(1), 0.8, CommitmentStatus.Planned, 0.0, new List<string>(), new Dictionary<string, object>(), DateTime.UtcNow, null);

        graph.AddCommitment(commitment);
    }

    #endregion
}
