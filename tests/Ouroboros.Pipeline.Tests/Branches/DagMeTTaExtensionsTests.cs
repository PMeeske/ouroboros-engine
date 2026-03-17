using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class DagMeTTaExtensionsTests
{
    private static PipelineBranch CreateBranch(string name = "test-branch")
    {
        return new PipelineBranch(name, new TrackedVectorStore(), DataSource.FromPath("."));
    }

    #region ToMeTTaFacts Tests

    [Fact]
    public void ToMeTTaFacts_WithEmptyBranch_ReturnsBasicFacts()
    {
        // Arrange
        var branch = CreateBranch("my-branch");

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().NotBeEmpty();
        facts.Should().Contain(f => f.Contains("Branch") && f.Contains("my-branch"));
        facts.Should().Contain(f => f.Contains("HasEventCount") && f.Contains("0"));
    }

    [Fact]
    public void ToMeTTaFacts_WithNullBranch_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => DagMeTTaExtensions.ToMeTTaFacts(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToMeTTaFacts_WithEvents_EncodesAllEvents()
    {
        // Arrange
        var branch = CreateBranch("event-branch");
        branch = branch.WithReasoning(new Draft("draft text"), "prompt");
        branch = branch.WithIngestEvent("source", new[] { "doc1", "doc2" });

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("HasEventCount") && f.Contains("2"));
        facts.Should().Contain(f => f.Contains("ReasoningEvent"));
        facts.Should().Contain(f => f.Contains("IngestEvent"));
    }

    [Fact]
    public void ToMeTTaFacts_WithMultipleEvents_EncodesOrdering()
    {
        // Arrange
        var branch = CreateBranch("ordered-branch");
        branch = branch.WithReasoning(new Draft("first"), "p1");
        branch = branch.WithReasoning(new Critique("second"), "p2");

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("Before"));
    }

    [Fact]
    public void ToMeTTaFacts_WithReasoningEvent_EncodesKind()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("text"), "prompt");

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("HasReasoningKind"));
    }

    [Fact]
    public void ToMeTTaFacts_WithToolCalls_EncodesToolUsage()
    {
        // Arrange
        var branch = CreateBranch();
        var tools = new List<ToolExecution>
        {
            new("search", "query", "result", DateTime.UtcNow)
        };
        branch = branch.WithReasoning(new Draft("text"), "prompt", tools);

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("UsesTool") && f.Contains("search"));
    }

    [Fact]
    public void ToMeTTaFacts_WithIngestEvent_EncodesIngestCount()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithIngestEvent("source", new[] { "d1", "d2", "d3" });

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("IngestedCount") && f.Contains("3"));
    }

    [Fact]
    public void ToMeTTaFacts_IncludesSourceInfo()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("HasSource"));
    }

    #endregion

    #region GetDagConstraintRules Tests

    [Fact]
    public void GetDagConstraintRules_ReturnsNonEmptyRuleList()
    {
        // Act
        var rules = DagMeTTaExtensions.GetDagConstraintRules();

        // Assert
        rules.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDagConstraintRules_ContainsTypeDeclarations()
    {
        // Act
        var rules = DagMeTTaExtensions.GetDagConstraintRules();

        // Assert
        rules.Should().Contain(r => r.Contains("BranchType"));
        rules.Should().Contain(r => r.Contains("EventType"));
    }

    [Fact]
    public void GetDagConstraintRules_ContainsAcyclicityConstraint()
    {
        // Act
        var rules = DagMeTTaExtensions.GetDagConstraintRules();

        // Assert
        rules.Should().Contain(r => r.Contains("Acyclic"));
    }

    [Fact]
    public void GetDagConstraintRules_ContainsValidForkRule()
    {
        // Act
        var rules = DagMeTTaExtensions.GetDagConstraintRules();

        // Assert
        rules.Should().Contain(r => r.Contains("ValidFork"));
    }

    #endregion

    #region EncodeConstraintQuery Tests

    [Fact]
    public void EncodeConstraintQuery_WithAcyclic_ReturnsAcyclicQuery()
    {
        // Act
        string query = DagMeTTaExtensions.EncodeConstraintQuery("acyclic", "branch-1");

        // Assert
        query.Should().Contain("Acyclic");
        query.Should().Contain("branch-1");
    }

    [Fact]
    public void EncodeConstraintQuery_WithValidOrdering_ReturnsOrderingQuery()
    {
        // Act
        string query = DagMeTTaExtensions.EncodeConstraintQuery("valid-ordering", "main");

        // Assert
        query.Should().Contain("Before");
        query.Should().Contain("EventAtIndex");
    }

    [Fact]
    public void EncodeConstraintQuery_WithNoToolConflicts_ReturnsToolQuery()
    {
        // Act
        string query = DagMeTTaExtensions.EncodeConstraintQuery("no-tool-conflicts", "test");

        // Assert
        query.Should().Contain("UsesTool");
    }

    [Fact]
    public void EncodeConstraintQuery_WithUnknownConstraint_ReturnsFallbackQuery()
    {
        // Act
        string query = DagMeTTaExtensions.EncodeConstraintQuery("custom-constraint", "branch-x");

        // Assert
        query.Should().Contain("CheckConstraint");
        query.Should().Contain("custom-constraint");
        query.Should().Contain("branch-x");
    }

    [Fact]
    public void EncodeConstraintQuery_IsCaseInsensitive()
    {
        // Act
        string lower = DagMeTTaExtensions.EncodeConstraintQuery("acyclic", "b");
        string upper = DagMeTTaExtensions.EncodeConstraintQuery("ACYCLIC", "b");

        // Assert
        lower.Should().Be(upper);
    }

    [Fact]
    public void EncodeConstraintQuery_WithNullConstraint_ThrowsArgumentException()
    {
        // Act
        Action act = () => DagMeTTaExtensions.EncodeConstraintQuery(null!, "branch");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncodeConstraintQuery_WithEmptyConstraint_ThrowsArgumentException()
    {
        // Act
        Action act = () => DagMeTTaExtensions.EncodeConstraintQuery("", "branch");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncodeConstraintQuery_WithNullBranchName_ThrowsArgumentException()
    {
        // Act
        Action act = () => DagMeTTaExtensions.EncodeConstraintQuery("acyclic", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncodeConstraintQuery_WithEmptyBranchName_ThrowsArgumentException()
    {
        // Act
        Action act = () => DagMeTTaExtensions.EncodeConstraintQuery("acyclic", "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region AddBranchFactsAsync Tests

    [Fact]
    public async Task AddBranchFactsAsync_WithNullEngine_ThrowsArgumentNullException()
    {
        // Arrange
        var branch = CreateBranch();
        Ouroboros.Abstractions.IMeTTaEngine? engine = null;

        // Act
        Func<Task> act = async () => await engine!.AddBranchFactsAsync(branch);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddBranchFactsAsync_WithNullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        using var step = new Ouroboros.Pipeline.Reasoning.HyperonReasoningStep("test");

        // Act
        Func<Task> act = async () => await step.Engine.AddBranchFactsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region VerifyDagConstraintAsync Tests

    [Fact]
    public async Task VerifyDagConstraintAsync_WithNullEngine_ThrowsArgumentNullException()
    {
        // Arrange
        Ouroboros.Abstractions.IMeTTaEngine? engine = null;

        // Act
        Func<Task> act = async () => await engine!.VerifyDagConstraintAsync("branch", "acyclic");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task VerifyDagConstraintAsync_WithEmptyBranchName_ThrowsArgumentException()
    {
        // Arrange
        using var step = new Ouroboros.Pipeline.Reasoning.HyperonReasoningStep("test");

        // Act
        Func<Task> act = async () => await step.Engine.VerifyDagConstraintAsync("", "acyclic");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task VerifyDagConstraintAsync_WithEmptyConstraint_ThrowsArgumentException()
    {
        // Arrange
        using var step = new Ouroboros.Pipeline.Reasoning.HyperonReasoningStep("test");

        // Act
        Func<Task> act = async () => await step.Engine.VerifyDagConstraintAsync("branch", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion
}
