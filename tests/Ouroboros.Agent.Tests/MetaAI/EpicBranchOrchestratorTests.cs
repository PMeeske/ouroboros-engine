using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class EpicBranchOrchestratorTests
{
    private readonly Mock<IDistributedOrchestrator> _mockDistributor = new();

    private EpicBranchOrchestrator CreateSut(EpicBranchConfig? config = null)
    {
        return new EpicBranchOrchestrator(_mockDistributor.Object, config);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullDistributor_ThrowsArgumentNullException()
    {
        var act = () => new EpicBranchOrchestrator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("distributor");
    }

    [Fact]
    public void Constructor_ValidDistributor_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullConfig_UsesDefaults()
    {
        var act = () => CreateSut(null);
        act.Should().NotThrow();
    }

    // === RegisterEpicAsync Tests ===

    [Fact]
    public async Task RegisterEpicAsync_EmptyTitle_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.RegisterEpicAsync(1, "", "desc", new List<int> { 10 });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("title");
    }

    [Fact]
    public async Task RegisterEpicAsync_NullSubIssues_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.RegisterEpicAsync(1, "Title", "desc", null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("sub-issue");
    }

    [Fact]
    public async Task RegisterEpicAsync_EmptySubIssues_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.RegisterEpicAsync(1, "Title", "desc", new List<int>());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("sub-issue");
    }

    [Fact]
    public async Task RegisterEpicAsync_ValidInput_ReturnsSuccess()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));

        var result = await sut.RegisterEpicAsync(1, "Epic Title", "Epic desc", new List<int> { 10, 20 });

        result.IsSuccess.Should().BeTrue();
        result.Value.EpicNumber.Should().Be(1);
        result.Value.Title.Should().Be("Epic Title");
        result.Value.SubIssueNumbers.Should().HaveCount(2);
    }

    [Fact]
    public async Task RegisterEpicAsync_AutoAssignEnabled_AssignsAgents()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: true, AutoCreateBranches: false));

        var result = await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });

        result.IsSuccess.Should().BeTrue();
        _mockDistributor.Verify(d => d.RegisterAgent(It.IsAny<AgentInfo>()), Times.AtLeastOnce);
    }

    // === AssignSubIssueAsync Tests ===

    [Fact]
    public async Task AssignSubIssueAsync_EpicNotFound_ReturnsFailure()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false));

        var result = await sut.AssignSubIssueAsync(999, 10);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task AssignSubIssueAsync_SubIssueNotInEpic_ReturnsFailure()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });

        var result = await sut.AssignSubIssueAsync(1, 999);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not part of epic");
    }

    [Fact]
    public async Task AssignSubIssueAsync_ValidAssignment_ReturnsSuccess()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });

        var result = await sut.AssignSubIssueAsync(1, 10, "agent-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedAgentId.Should().Be("agent-1");
        result.Value.IssueNumber.Should().Be(10);
    }

    [Fact]
    public async Task AssignSubIssueAsync_NoPreferredAgent_GeneratesAgentId()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });

        var result = await sut.AssignSubIssueAsync(1, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.AssignedAgentId.Should().Contain("1").And.Contain("10");
    }

    [Fact]
    public async Task AssignSubIssueAsync_AutoCreateBranches_CreatesBranch()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: true));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });

        var result = await sut.AssignSubIssueAsync(1, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SubIssueStatus.BranchCreated);
    }

    // === GetSubIssueAssignments Tests ===

    [Fact]
    public async Task GetSubIssueAssignments_AfterAssignment_ReturnsAssignments()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10, 20 });
        await sut.AssignSubIssueAsync(1, 10);
        await sut.AssignSubIssueAsync(1, 20);

        var assignments = sut.GetSubIssueAssignments(1);

        assignments.Should().HaveCount(2);
    }

    [Fact]
    public void GetSubIssueAssignments_NoAssignments_ReturnsEmpty()
    {
        var sut = CreateSut();

        var assignments = sut.GetSubIssueAssignments(999);

        assignments.Should().BeEmpty();
    }

    // === GetSubIssueAssignment Tests ===

    [Fact]
    public async Task GetSubIssueAssignment_ExistingAssignment_ReturnsAssignment()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);

        var assignment = sut.GetSubIssueAssignment(1, 10);

        assignment.Should().NotBeNull();
        assignment!.IssueNumber.Should().Be(10);
    }

    [Fact]
    public void GetSubIssueAssignment_NonExistent_ReturnsNull()
    {
        var sut = CreateSut();

        var assignment = sut.GetSubIssueAssignment(999, 10);

        assignment.Should().BeNull();
    }

    // === UpdateSubIssueStatus Tests ===

    [Fact]
    public async Task UpdateSubIssueStatus_ValidUpdate_ReturnsSuccess()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);

        var result = sut.UpdateSubIssueStatus(1, 10, SubIssueStatus.InProgress);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(SubIssueStatus.InProgress);
    }

    [Fact]
    public async Task UpdateSubIssueStatus_Completed_SetsCompletedAt()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);

        var result = sut.UpdateSubIssueStatus(1, 10, SubIssueStatus.Completed);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateSubIssueStatus_NonExistent_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = sut.UpdateSubIssueStatus(999, 10, SubIssueStatus.InProgress);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    // === ExecuteSubIssueAsync Tests ===

    [Fact]
    public async Task ExecuteSubIssueAsync_NonExistentAssignment_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteSubIssueAsync(999, 10, _ =>
            Task.FromResult(Result<SubIssueAssignment, string>.Failure("fail")));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteSubIssueAsync_AlreadyInProgress_ReturnsFailure()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);
        sut.UpdateSubIssueStatus(1, 10, SubIssueStatus.InProgress);

        var result = await sut.ExecuteSubIssueAsync(1, 10, _ =>
            Task.FromResult(Result<SubIssueAssignment, string>.Success(default!)));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already in progress");
    }

    [Fact]
    public async Task ExecuteSubIssueAsync_SuccessfulWork_UpdatesStatusToCompleted()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);

        var result = await sut.ExecuteSubIssueAsync(1, 10, assignment =>
            Task.FromResult(Result<SubIssueAssignment, string>.Success(assignment)));

        result.IsSuccess.Should().BeTrue();
        var assignment = sut.GetSubIssueAssignment(1, 10);
        assignment!.Status.Should().Be(SubIssueStatus.Completed);
    }

    [Fact]
    public async Task ExecuteSubIssueAsync_FailedWork_UpdatesStatusToFailed()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);

        var result = await sut.ExecuteSubIssueAsync(1, 10, _ =>
            Task.FromResult(Result<SubIssueAssignment, string>.Failure("work failed")));

        result.IsFailure.Should().BeTrue();
        var assignment = sut.GetSubIssueAssignment(1, 10);
        assignment!.Status.Should().Be(SubIssueStatus.Failed);
    }

    [Fact]
    public async Task ExecuteSubIssueAsync_WorkThrows_UpdatesStatusToFailed()
    {
        var sut = CreateSut(new EpicBranchConfig(AutoAssignAgents: false, AutoCreateBranches: false));
        await sut.RegisterEpicAsync(1, "Epic", "desc", new List<int> { 10 });
        await sut.AssignSubIssueAsync(1, 10);

        var result = await sut.ExecuteSubIssueAsync(1, 10, _ =>
            throw new InvalidOperationException("boom"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Execution failed");
    }
}
