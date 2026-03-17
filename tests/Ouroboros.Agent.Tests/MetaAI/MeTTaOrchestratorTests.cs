using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class MeTTaOrchestratorTests
{
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockLlm = new();
    private readonly Mock<IMemoryStore> _mockMemory = new();
    private readonly Mock<ISkillRegistry> _mockSkills = new();
    private readonly Mock<IUncertaintyRouter> _mockRouter = new();
    private readonly Mock<ISafetyGuard> _mockSafety = new();
    private readonly Mock<IMeTTaEngine> _mockEngine = new();

    private MeTTaOrchestrator CreateSut(FormMeTTaBridge? formBridge = null)
    {
        return new MeTTaOrchestrator(
            _mockLlm.Object,
            new ToolRegistry(),
            _mockMemory.Object,
            _mockSkills.Object,
            _mockRouter.Object,
            _mockSafety.Object,
            _mockEngine.Object,
            formBridge);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullLlm_ThrowsArgumentNullException()
    {
        var act = () => new MeTTaOrchestrator(
            null!, new ToolRegistry(), _mockMemory.Object,
            _mockSkills.Object, _mockRouter.Object, _mockSafety.Object, _mockEngine.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    [Fact]
    public void Constructor_NullMeTTaEngine_ThrowsArgumentNullException()
    {
        var act = () => new MeTTaOrchestrator(
            _mockLlm.Object, new ToolRegistry(), _mockMemory.Object,
            _mockSkills.Object, _mockRouter.Object, _mockSafety.Object, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("mettaEngine");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    // === FormBridge Properties ===

    [Fact]
    public void FormBridge_NoFormBridge_ReturnsNull()
    {
        var sut = CreateSut();

        sut.FormBridge.Should().BeNull();
        sut.FormReasoningEnabled.Should().BeFalse();
    }

    // === DrawDistinction Tests ===

    [Fact]
    public void DrawDistinction_NoFormBridge_ReturnsNull()
    {
        var sut = CreateSut();

        var result = sut.DrawDistinction("test-context");

        result.Should().BeNull();
    }

    // === IsDistinctionCertain Tests ===

    [Fact]
    public void IsDistinctionCertain_NoFormBridge_ReturnsTrue()
    {
        var sut = CreateSut();

        var result = sut.IsDistinctionCertain("test-context");

        result.Should().BeTrue();
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
    public async Task PlanAsync_ValidGoal_ReturnsSuccess()
    {
        var sut = CreateSut();
        SetupPlanningDependencies();

        var result = await sut.PlanAsync("test goal");

        result.IsSuccess.Should().BeTrue();
        result.Value.Goal.Should().Be("test goal");
    }

    [Fact]
    public async Task PlanAsync_SafetyCheckFails_ReturnsFailure()
    {
        var sut = CreateSut();
        SetupPlanningDependencies(safetyPasses: false);

        var result = await sut.PlanAsync("test goal");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("safety check");
    }

    [Fact]
    public async Task PlanAsync_RecordsPlannerMetric()
    {
        var sut = CreateSut();
        SetupPlanningDependencies();

        await sut.PlanAsync("test goal");

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("planner");
    }

    // === ExecuteAsync Tests ===

    [Fact]
    public async Task ExecuteAsync_EmptyPlan_ReturnsSuccessWithNoSteps()
    {
        var sut = CreateSut();
        var plan = new Plan("goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);

        var result = await sut.ExecuteAsync(plan);

        result.IsSuccess.Should().BeTrue();
        result.Value.StepResults.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ValidPlan_ExecutesAllSteps()
    {
        var sut = CreateSut();
        var plan = CreateSimplePlan("test");

        // Setup tool registry to not find tools so it falls through
        SetupExecutionDependencies();

        var result = await sut.ExecuteAsync(plan);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RecordsExecutorMetric()
    {
        var sut = CreateSut();
        var plan = new Plan("goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);

        await sut.ExecuteAsync(plan);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("executor");
    }

    // === VerifyAsync Tests ===

    [Fact]
    public async Task VerifyAsync_ValidExecution_ReturnsVerification()
    {
        var sut = CreateSut();
        var execution = CreateExecution();

        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"verified\": true, \"quality_score\": 0.85, \"issues\": [], \"improvements\": []}");

        _mockEngine.Setup(e => e.VerifyPlanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        var result = await sut.VerifyAsync(execution);

        result.IsSuccess.Should().BeTrue();
        result.Value.Verified.Should().BeTrue();
        result.Value.QualityScore.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public async Task VerifyAsync_InvalidJson_FallsBackToDefaults()
    {
        var sut = CreateSut();
        var execution = CreateExecution();

        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json");

        _mockEngine.Setup(e => e.VerifyPlanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        var result = await sut.VerifyAsync(execution);

        result.IsSuccess.Should().BeTrue();
        // Should fall back to execution success-based defaults
        result.Value.Verified.Should().BeTrue(); // execution.Success is true
    }

    [Fact]
    public async Task VerifyAsync_MeTTaVerificationIncluded_AppendedToImprovements()
    {
        var sut = CreateSut();
        var execution = CreateExecution();

        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"verified\": true, \"quality_score\": 0.8, \"issues\": [], \"improvements\": []}");

        _mockEngine.Setup(e => e.VerifyPlanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool, string>.Success(true));

        var result = await sut.VerifyAsync(execution);

        result.IsSuccess.Should().BeTrue();
        result.Value.Improvements.Should().Contain(i => i.Contains("MeTTa verification"));
    }

    // === LearnFromExecution Tests ===

    [Fact]
    public void LearnFromExecution_ValidVerification_RecordsMetric()
    {
        var sut = CreateSut();
        var verification = CreateVerification(true, 0.8);

        sut.LearnFromExecution(verification);

        var metrics = sut.GetMetrics();
        metrics.Should().ContainKey("learner");
    }

    // === GetMetrics Tests ===

    [Fact]
    public void GetMetrics_Initially_ReturnsEmptyOrMinimalMetrics()
    {
        var sut = CreateSut();
        var metrics = sut.GetMetrics();
        metrics.Should().NotBeNull();
    }

    // === Helper Methods ===

    private void SetupPlanningDependencies(bool safetyPasses = true)
    {
        _mockMemory.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IEnumerable<Experience>, string>.Failure("no experiences"));

        _mockSkills.Setup(s => s.FindMatchingSkillsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new List<Skill>());

        // Return valid JSON plan
        _mockLlm.Setup(l => l.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[{\"action\": \"test_action\", \"parameters\": {}, \"expected_outcome\": \"test\", \"confidence\": 0.8}]");

        // MeTTa translation (fire and forget, can return failure without breaking flow)
        _mockEngine.Setup(e => e.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("ok"));

        if (safetyPasses)
        {
            _mockSafety.Setup(s => s.CheckSafety(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<PermissionLevel>()))
                .Returns(SafetyCheckResult.Allowed("Safe"));
        }
        else
        {
            _mockSafety.Setup(s => s.CheckSafety(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<PermissionLevel>()))
                .Returns(new SafetyCheckResult(false, new List<string> { "unsafe" }));
        }
    }

    private void SetupExecutionDependencies()
    {
        _mockEngine.Setup(e => e.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("ok"));
    }

    private static Plan CreateSimplePlan(string goal)
    {
        return new Plan(
            goal,
            new List<PlanStep>
            {
                new PlanStep("test_tool", new Dictionary<string, object>(), "expected", 0.8)
            },
            new Dictionary<string, double> { ["overall"] = 0.8 },
            DateTime.UtcNow);
    }

    private static PlanExecutionResult CreateExecution()
    {
        var plan = CreateSimplePlan("test");
        var stepResults = new List<StepResult>
        {
            new StepResult(plan.Steps[0], true, "output", null, TimeSpan.FromMilliseconds(50), new Dictionary<string, object>())
        };
        return new PlanExecutionResult(plan, stepResults, true, "output", new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
    }

    private static PlanVerificationResult CreateVerification(bool verified, double qualityScore)
    {
        return new PlanVerificationResult(CreateExecution(), verified, qualityScore,
            new List<string>(), new List<string>(), DateTime.UtcNow);
    }
}
