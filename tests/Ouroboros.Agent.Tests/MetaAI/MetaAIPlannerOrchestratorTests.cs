using FluentAssertions;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MetaAIPlannerOrchestratorTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockLlm = new();
    private readonly Mock<IMemoryStore> _mockMemory = new();
    private readonly Mock<ISkillRegistry> _mockSkills = new();
    private readonly Mock<IUncertaintyRouter> _mockRouter = new();
    private readonly Mock<ISafetyGuard> _mockSafety = new();
    private readonly Mock<IEthicsFramework> _mockEthics = new();

    private MetaAIPlannerOrchestrator CreateSut(
        IHumanApprovalProvider? approvalProvider = null,
        ISkillExtractor? skillExtractor = null)
    {
        return new MetaAIPlannerOrchestrator(
            _mockLlm.Object,
            new ToolRegistry(),
            _mockMemory.Object,
            _mockSkills.Object,
            _mockRouter.Object,
            _mockSafety.Object,
            _mockEthics.Object,
            approvalProvider,
            skillExtractor);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullLlm_ThrowsArgumentNullException()
    {
        var act = () => new MetaAIPlannerOrchestrator(
            null!,
            new ToolRegistry(),
            _mockMemory.Object,
            _mockSkills.Object,
            _mockRouter.Object,
            _mockSafety.Object,
            _mockEthics.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    [Fact]
    public void Constructor_NullTools_ThrowsArgumentNullException()
    {
        var act = () => new MetaAIPlannerOrchestrator(
            _mockLlm.Object,
            null!,
            _mockMemory.Object,
            _mockSkills.Object,
            _mockRouter.Object,
            _mockSafety.Object,
            _mockEthics.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tools");
    }

    [Fact]
    public void Constructor_NullMemory_ThrowsArgumentNullException()
    {
        var act = () => new MetaAIPlannerOrchestrator(
            _mockLlm.Object,
            new ToolRegistry(),
            null!,
            _mockSkills.Object,
            _mockRouter.Object,
            _mockSafety.Object,
            _mockEthics.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("memory");
    }

    [Fact]
    public void Constructor_NullSafety_ThrowsArgumentNullException()
    {
        var act = () => new MetaAIPlannerOrchestrator(
            _mockLlm.Object,
            new ToolRegistry(),
            _mockMemory.Object,
            _mockSkills.Object,
            _mockRouter.Object,
            null!,
            _mockEthics.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("safety");
    }

    [Fact]
    public void Constructor_NullEthics_ThrowsArgumentNullException()
    {
        var act = () => new MetaAIPlannerOrchestrator(
            _mockLlm.Object,
            new ToolRegistry(),
            _mockMemory.Object,
            _mockSkills.Object,
            _mockRouter.Object,
            _mockSafety.Object,
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("ethics");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    // === PlanAsync Tests ===

    [Fact]
    public async Task PlanAsync_EmptyGoal_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.PlanAsync("");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task PlanAsync_WhitespaceGoal_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.PlanAsync("   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanAsync_ValidGoal_QueriesMemoryAndSkills()
    {
        var sut = CreateSut();
        SetupPlanningDependencies();

        await sut.PlanAsync("test goal");

        _mockMemory.Verify(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSkills.Verify(s => s.FindMatchingSkillsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()), Times.Once);
    }

    [Fact]
    public async Task PlanAsync_EthicsBlocksPlan_ReturnsFailure()
    {
        var sut = CreateSut();
        SetupPlanningDependencies(ethicsPermitted: false);

        var result = await sut.PlanAsync("test goal");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ethics");
    }

    [Fact]
    public async Task PlanAsync_SafetyBlocksStep_ReturnsFailure()
    {
        var sut = CreateSut();
        SetupPlanningDependencies(safetyPasses: false);

        var result = await sut.PlanAsync("test goal");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("safety check");
    }

    [Fact]
    public async Task PlanAsync_ValidGoal_RecordsMetric()
    {
        var sut = CreateSut();
        SetupPlanningDependencies();

        await sut.PlanAsync("test goal");

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("planner");
    }

    // === ExecuteAsync Tests ===

    [Fact]
    public async Task ExecuteAsync_NullPlan_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task ExecuteAsync_ValidPlan_ReturnsSuccess()
    {
        var sut = CreateSut();
        var plan = CreateSimplePlan("test goal");
        _mockSafety.Setup(s => s.SandboxStep(It.IsAny<PlanStep>())).Returns((PlanStep step) => step);
        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("executed");

        var result = await sut.ExecuteAsync(plan);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidPlan_RecordsMetric()
    {
        var sut = CreateSut();
        var plan = CreateSimplePlan("test goal");
        _mockSafety.Setup(s => s.SandboxStep(It.IsAny<PlanStep>())).Returns((PlanStep step) => step);
        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("done");

        await sut.ExecuteAsync(plan);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("executor");
    }

    // === VerifyAsync Tests ===

    [Fact]
    public async Task VerifyAsync_NullExecution_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.VerifyAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task VerifyAsync_ValidExecution_ReturnsVerification()
    {
        var sut = CreateSut();
        var execution = CreateExecution();
        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("VERIFIED: yes\nQUALITY_SCORE: 0.9");

        var result = await sut.VerifyAsync(execution);

        result.IsSuccess.Should().BeTrue();
        result.Value.Verified.Should().BeTrue();
        result.Value.QualityScore.Should().BeApproximately(0.9, 0.01);
    }

    [Fact]
    public async Task VerifyAsync_NotVerified_ReturnsFalse()
    {
        var sut = CreateSut();
        var execution = CreateExecution();
        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("VERIFIED: no\nQUALITY_SCORE: 0.3");

        var result = await sut.VerifyAsync(execution);

        result.IsSuccess.Should().BeTrue();
        result.Value.Verified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_LlmThrows_ReturnsFailure()
    {
        var sut = CreateSut();
        var execution = CreateExecution();
        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM error"));

        var result = await sut.VerifyAsync(execution);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Verification failed");
    }

    // === LearnFromExecution Tests ===

    [Fact]
    public void LearnFromExecution_NullVerification_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.LearnFromExecution(null!);

        act.Should().NotThrow();
    }

    [Fact]
    public void LearnFromExecution_ValidVerification_RecordsMetric()
    {
        var sut = CreateSut();
        var verification = CreateVerification(verified: true, qualityScore: 0.9);

        sut.LearnFromExecution(verification);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("learning");
    }

    [Fact]
    public void LearnFromExecution_HighQualityVerified_AttemptsSkillExtraction()
    {
        var mockExtractor = new Mock<ISkillExtractor>();
        mockExtractor.Setup(e => e.ShouldExtractSkillAsync(It.IsAny<PlanVerificationResult>()))
            .ReturnsAsync(false);

        var sut = CreateSut(skillExtractor: mockExtractor.Object);
        var verification = CreateVerification(verified: true, qualityScore: 0.9);

        sut.LearnFromExecution(verification);

        // Give the fire-and-forget task a moment
        Thread.Sleep(100);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("learning");
    }

    // === GetMetrics Tests ===

    [Fact]
    public void GetMetrics_Initially_ReturnsEmptyDictionary()
    {
        var sut = CreateSut();

        var metrics = sut.GetMetrics();

        metrics.Should().BeEmpty();
    }

    [Fact]
    public void GetMetrics_AfterOperations_ReturnsCopy()
    {
        var sut = CreateSut();
        sut.LearnFromExecution(CreateVerification(true, 0.8));

        var metrics1 = sut.GetMetrics();
        var metrics2 = sut.GetMetrics();

        metrics1.Should().NotBeSameAs(metrics2);
    }

    // === Helper Methods ===

    private void SetupPlanningDependencies(
        bool ethicsPermitted = true,
        bool safetyPasses = true)
    {
        _mockMemory.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("no experiences"));

        _mockSkills.Setup(s => s.FindMatchingSkillsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new List<Skill>());

        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("STEP 1: test_action\nPARAMETERS: {}\nEXPECTED: test\nCONFIDENCE: 0.8");

        var clearance = new EthicalClearance
        {
            IsPermitted = ethicsPermitted,
            Level = EthicalClearanceLevel.Permitted,
            Reasoning = ethicsPermitted ? "Allowed" : "Blocked by ethics",
            Concerns = new List<EthicalConcern>()
        };

        _mockEthics.Setup(e => e.EvaluatePlanAsync(It.IsAny<PlanContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(clearance));

        if (safetyPasses)
        {
            _mockSafety.Setup(s => s.CheckSafety(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<PermissionLevel>()))
                .Returns(SafetyCheckResult.Allowed("Safe"));
        }
        else
        {
            _mockSafety.Setup(s => s.CheckSafety(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<PermissionLevel>()))
                .Returns(new SafetyCheckResult(false, new List<string> { "unsafe operation" }));
        }
    }

    private static Plan CreateSimplePlan(string goal)
    {
        return new Plan(
            goal,
            new List<PlanStep>
            {
                new PlanStep("llm_direct", new Dictionary<string, object> { ["goal"] = goal }, "Direct response", 0.8)
            },
            new Dictionary<string, double> { ["overall"] = 0.8 },
            DateTime.UtcNow);
    }

    private static PlanExecutionResult CreateExecution()
    {
        var plan = CreateSimplePlan("test");
        var stepResults = new List<StepResult>
        {
            new StepResult(
                plan.Steps[0],
                true,
                "step output",
                null,
                TimeSpan.FromMilliseconds(100),
                new Dictionary<string, object>())
        };

        return new PlanExecutionResult(plan, stepResults, true, "final output",
            new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
    }

    private static PlanVerificationResult CreateVerification(bool verified, double qualityScore)
    {
        var execution = CreateExecution();
        return new PlanVerificationResult(
            execution, verified, qualityScore,
            new List<string>(), new List<string>(), DateTime.UtcNow);
    }
}
