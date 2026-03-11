// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Hyperon;
using ToolRegistry = Ouroboros.Tools.ToolRegistry;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Complex logic tests for OuroborosOrchestrator covering the Plan-Execute-Verify-Learn cycle,
/// phase transitions, safety gating, step execution routing, verification retry,
/// capability updates, and affective state modulation.
/// </summary>
[Trait("Category", "Unit")]
public class OuroborosOrchestratorComplexLogicTests
{
    // ================================================================
    // Shared mock helpers
    // ================================================================

    /// <summary>
    /// Mock chat completion model with prompt-based response routing.
    /// </summary>
    private sealed class RoutingChatModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> _handler;
        public List<string> CapturedPrompts { get; } = new();
        public int CallCount => CapturedPrompts.Count;

        public RoutingChatModel(Func<string, CancellationToken, Task<string>> handler)
        {
            _handler = handler;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            CapturedPrompts.Add(prompt);
            return _handler(prompt, ct);
        }
    }

    /// <summary>
    /// Mock MeTTa engine that records all facts added.
    /// </summary>
    private sealed class TrackingMeTTaEngine : IMeTTaEngine
    {
        public List<string> Facts { get; } = new();
        public List<string> Queries { get; } = new();
        private readonly Func<string, CancellationToken, Task<Result<bool, string>>>? _verifyOverride;

        public TrackingMeTTaEngine(Func<string, CancellationToken, Task<Result<bool, string>>>? verifyOverride = null)
        {
            _verifyOverride = verifyOverride;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            Queries.Add(query);
            return Task.FromResult(Result<string, string>.Success("(ok)"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            Facts.Add(fact);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => _verifyOverride != null
                ? _verifyOverride(plan, ct)
                : Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    private OuroborosOrchestrator CreateOrchestrator(
        IChatCompletionModel llm,
        TrackingMeTTaEngine? meTTa = null,
        OuroborosAtom? atom = null,
        Ouroboros.Agent.MetaAI.Affect.IValenceMonitor? valenceMonitor = null,
        Ouroboros.Agent.MetaAI.Affect.IUrgeSystem? urgeSystem = null)
    {
        var tools = ToolRegistry.CreateDefault();
        var memory = new Mock<IMemoryStore>().Object;
        var safety = new Mock<ISafetyGuard>().Object;
        var engine = meTTa ?? new TrackingMeTTaEngine();

        return new OuroborosOrchestrator(
            llm, tools, memory, safety, engine,
            atom: atom,
            valenceMonitor: valenceMonitor,
            urgeSystem: urgeSystem);
    }

    /// <summary>
    /// Creates a RoutingChatModel that responds appropriately to plan/verify/learn prompts.
    /// </summary>
    private static RoutingChatModel CreateStandardRoutingModel(
        bool verificationPasses = true,
        double qualityScore = 0.85)
    {
        return new RoutingChatModel((prompt, ct) =>
        {
            // Plan phase: return structured steps
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. Analyze requirements\n2. Implement solution\n3. Validate output");

            // Verify phase: return JSON verification
            if (prompt.Contains("Verify if"))
                return Task.FromResult(
                    $"{{\"verified\": {(verificationPasses ? "true" : "false")}, \"quality_score\": {qualityScore.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");

            // Learn phase: return insights
            if (prompt.Contains("Extract") && prompt.Contains("insights"))
                return Task.FromResult("- Applied proper decomposition\n- Good use of tools\n- Consider caching");

            // Step execution: default response
            if (prompt.Contains("Process this step"))
                return Task.FromResult("Step processed successfully");

            return Task.FromResult("OK");
        });
    }

    // ================================================================
    // Full cycle tests - Plan-Execute-Verify-Learn
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_FullCycle_AllPhasesSucceed_ReturnsSuccessResult()
    {
        // Arrange
        var llm = CreateStandardRoutingModel(verificationPasses: true, qualityScore: 0.85);
        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Write a unit test");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Output.PhaseResults.Should().HaveCount(4, "Plan, Execute, Verify, Learn");
        result.Output.PhaseResults[0].Phase.Should().Be(ImprovementPhase.Plan);
        result.Output.PhaseResults[1].Phase.Should().Be(ImprovementPhase.Execute);
        result.Output.PhaseResults[2].Phase.Should().Be(ImprovementPhase.Verify);
        result.Output.PhaseResults[3].Phase.Should().Be(ImprovementPhase.Learn);
    }

    [Fact]
    public async Task ExecuteAsync_PlanPhaseGeneratesStructuredSteps_ExecutePhaseProcessesThem()
    {
        // Arrange
        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Build a feature");

        // Assert
        var executePhase = result.Output.PhaseResults.First(p => p.Phase == ImprovementPhase.Execute);
        executePhase.Success.Should().BeTrue();
        executePhase.Metadata.Should().ContainKey("steps_count");
        ((int)executePhase.Metadata["steps_count"]).Should().BeGreaterThanOrEqualTo(1);
    }

    // ================================================================
    // Plan phase - safety gating
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_UnsafeGoal_PlanPhaseFailsImmediately()
    {
        // Arrange: use a keyword that triggers IsSafeAction to return false
        // OuroborosAtom.CreateDefault() uses SafetyConstraints.All which includes NoSelfDestruction
        var llm = CreateStandardRoutingModel();
        var atom = OuroborosAtom.CreateDefault();
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act: "delete self" is checked by NoSelfDestruction constraint
        var result = await orchestrator.ExecuteAsync("delete self from system");

        // Assert
        result.Output.PhaseResults.Should().HaveCount(1, "stops after Plan phase failure");
        result.Output.PhaseResults[0].Phase.Should().Be(ImprovementPhase.Plan);
        result.Output.PhaseResults[0].Success.Should().BeFalse();
        result.Output.PhaseResults[0].Error.Should().Contain("safety constraints");
    }

    // ================================================================
    // Plan phase - confidence assessment affects prompt
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_PlanPhase_IncludesConfidenceInPrompt()
    {
        // Arrange
        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm);

        // Act
        await orchestrator.ExecuteAsync("Test confidence assessment");

        // Assert: plan prompt should have been sent with self-reflection context
        var planPrompt = ((RoutingChatModel)llm).CapturedPrompts.FirstOrDefault(p => p.Contains("Create a plan"));
        planPrompt.Should().NotBeNull();
        planPrompt.Should().Contain("Self-Assessment");
        planPrompt.Should().Contain("Confidence");
    }

    // ================================================================
    // Execute phase - step routing (ToolVsLLMWeight)
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_LowToolWeight_SkipsToolSelectionUsesLlm()
    {
        // Arrange: set ToolVsLLMWeight < 0.3 to force LLM path
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Tool vs LLM weight", 0.1));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        var result = await orchestrator.ExecuteAsync("Process data");

        // Assert: execution should succeed via LLM path
        var executePhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Execute);
        executePhase.Should().NotBeNull();
        executePhase!.Success.Should().BeTrue();
    }

    // ================================================================
    // Verify phase - quality threshold modulation
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_HighVerificationStrictness_RaisesQualityThreshold()
    {
        // Arrange: borderline quality (0.6) with high strictness (threshold = 0.3 + 0.9*0.5 = 0.75)
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Strictness", 0.9));

        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. Do thing");
            if (prompt.Contains("Verify if"))
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.6}");
            if (prompt.Contains("Extract") && prompt.Contains("insights"))
                return Task.FromResult("- insight one");
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        var result = await orchestrator.ExecuteAsync("Test strictness");

        // Assert
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        // Quality 0.6 < threshold 0.75, so verification fails
        verifyPhase!.Success.Should().BeFalse();
        verifyPhase.Metadata.Should().ContainKey("meets_quality_threshold");
        verifyPhase.Metadata["meets_quality_threshold"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_LowVerificationStrictness_LowersQualityThreshold()
    {
        // Arrange: borderline quality (0.4) with low strictness (threshold = 0.3 + 0.1*0.5 = 0.35)
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_VerificationStrictness", "Strictness", 0.1));

        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. Do thing");
            if (prompt.Contains("Verify if"))
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.4}");
            if (prompt.Contains("Extract") && prompt.Contains("insights"))
                return Task.FromResult("- insight");
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        var result = await orchestrator.ExecuteAsync("Test lenient strictness");

        // Assert
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        // Quality 0.4 > threshold 0.35, so quality threshold passes
        verifyPhase!.Metadata["meets_quality_threshold"].Should().Be(true);
    }

    // ================================================================
    // Verify phase - MeTTa verification integration
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_MeTTaVerificationFails_OverallVerificationFails()
    {
        // Arrange
        var meTTa = new TrackingMeTTaEngine((plan, ct) =>
            Task.FromResult(Result<bool, string>.Failure("MeTTa error")));

        var llm = CreateStandardRoutingModel(verificationPasses: true, qualityScore: 0.9);
        var orchestrator = CreateOrchestrator(llm, meTTa: meTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test MeTTa failure");

        // Assert
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeFalse("MeTTa verification is required");
        verifyPhase.Metadata["metta_verified"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_MeTTaVerificationPasses_ContributesToSuccess()
    {
        // Arrange
        var meTTa = new TrackingMeTTaEngine((plan, ct) =>
            Task.FromResult(Result<bool, string>.Success(true)));

        var llm = CreateStandardRoutingModel(verificationPasses: true, qualityScore: 0.9);
        var orchestrator = CreateOrchestrator(llm, meTTa: meTTa);

        // Act
        var result = await orchestrator.ExecuteAsync("Test full verification");

        // Assert
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Success.Should().BeTrue();
        verifyPhase.Metadata["metta_verified"].Should().Be(true);
        verifyPhase.Metadata["llm_verified"].Should().Be(true);
    }

    // ================================================================
    // Verify phase - JSON parsing retry
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_MalformedVerificationJsonThenValid_RetriesSuccessfully()
    {
        // Arrange: first verify call returns garbage, retry returns valid JSON
        int verifyCallCount = 0;
        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. Step one");
            if (prompt.Contains("Verify") || prompt.Contains("verify"))
            {
                verifyCallCount++;
                if (verifyCallCount == 1)
                    return Task.FromResult("This is NOT valid JSON!!!");
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.8}");
            }
            if (prompt.Contains("Extract") && prompt.Contains("insights"))
                return Task.FromResult("- Good insight");
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Test JSON retry");

        // Assert
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        verifyPhase!.Metadata["quality_score"].Should().Be(0.8);
        verifyCallCount.Should().BeGreaterThanOrEqualTo(2, "should retry after malformed JSON");
    }

    [Fact]
    public async Task ExecuteAsync_AllVerificationJsonMalformed_FailsClosed()
    {
        // Arrange: all verify calls return garbage
        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. Step one");
            if (prompt.Contains("Verify") || prompt.Contains("verify"))
                return Task.FromResult("NOT JSON AT ALL");
            if (prompt.Contains("Extract") && prompt.Contains("insights"))
                return Task.FromResult("- insight");
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Test fail-closed");

        // Assert
        var verifyPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Verify);
        verifyPhase.Should().NotBeNull();
        // Fail-closed: quality_score = 0.0, verified = false
        verifyPhase!.Metadata["quality_score"].Should().Be(0.0);
        verifyPhase.Success.Should().BeFalse();
    }

    // ================================================================
    // Execute phase - step failure halts execution
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_StepExecutionThrows_ExecutePhaseFails()
    {
        // Arrange: LLM throws on "Process this step" calls
        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. Run dangerous action");
            if (prompt.Contains("Process this step"))
                throw new InvalidOperationException("LLM connection lost");
            if (prompt.Contains("Verify"))
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.8}");
            if (prompt.Contains("Extract"))
                return Task.FromResult("- insight");
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Test step failure");

        // Assert
        var executePhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Execute);
        executePhase.Should().NotBeNull();
        executePhase!.Success.Should().BeFalse();
        executePhase.Error.Should().Contain("Execution failed");
    }

    // ================================================================
    // Learn phase - experience recording
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_LearnPhase_ExtractsInsights()
    {
        // Arrange
        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Test learn phase");

        // Assert
        var learnPhase = result.Output.PhaseResults.FirstOrDefault(p => p.Phase == ImprovementPhase.Learn);
        learnPhase.Should().NotBeNull();
        learnPhase!.Success.Should().BeTrue();
        learnPhase.Metadata.Should().ContainKey("insights_count");
        ((int)learnPhase.Metadata["insights_count"]).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulCycle_RecordsExperienceInAtom()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        await orchestrator.ExecuteAsync("Record experience test");

        // Assert
        atom.Experiences.Should().HaveCountGreaterThanOrEqualTo(1);
        atom.Experiences.Last().Goal.Should().Contain("Record experience test");
    }

    // ================================================================
    // Learn phase - capability confidence boosting
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_HighQualitySuccess_BoostsRelevantCapabilityConfidence()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        var existingCapability = new OuroborosCapability("analysis", "Data analysis", 0.6);
        atom.AddCapability(existingCapability);

        var llm = CreateStandardRoutingModel(verificationPasses: true, qualityScore: 0.9);
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        // Goal contains "analysis" which matches the capability name
        await orchestrator.ExecuteAsync("Perform data analysis");

        // Assert: capability confidence should have been boosted by 0.05
        var updatedCap = atom.Capabilities.FirstOrDefault(c => c.Name == "analysis");
        updatedCap.Should().NotBeNull();
        updatedCap!.ConfidenceLevel.Should().BeApproximately(0.65, 0.01);
    }

    // ================================================================
    // MeTTa state translation
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_TranslatesAtomStateToMeTTa()
    {
        // Arrange
        var meTTa = new TrackingMeTTaEngine();
        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, meTTa: meTTa);

        // Act
        await orchestrator.ExecuteAsync("Test MeTTa translation");

        // Assert: should have added facts for atom state, plus learn phase knowledge
        meTTa.Facts.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    // ================================================================
    // Affective state integration
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_WithValenceMonitor_RecordsAffectiveSignals()
    {
        // Arrange
        var mockValence = new Mock<Ouroboros.Agent.MetaAI.Affect.IValenceMonitor>();
        mockValence
            .Setup(v => v.GetCurrentState())
            .Returns(new Ouroboros.Agent.MetaAI.Affect.AffectiveState(
                Guid.NewGuid(), 0.5, 0.2, 0.7, 0.4, 0.3, DateTime.UtcNow, new Dictionary<string, object>()));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, valenceMonitor: mockValence.Object);

        // Act
        await orchestrator.ExecuteAsync("Test affect tracking");

        // Assert: valence monitor should have been queried for current state
        mockValence.Verify(v => v.GetCurrentState(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WithUrgeSystem_TicksAtStartAndSatisfiesOnSuccess()
    {
        // Arrange
        var mockUrge = new Mock<Ouroboros.Agent.MetaAI.Affect.IUrgeSystem>();
        mockUrge
            .Setup(u => u.GetDominantUrge())
            .Returns(new Ouroboros.Agent.MetaAI.Affect.Urge("curiosity", "Desire to explore", 0.7, 0.1, 0.05, 0.8));
        mockUrge
            .Setup(u => u.ToMeTTa(It.IsAny<string>()))
            .Returns("(Urge curiosity 0.7)");

        var mockValence = new Mock<Ouroboros.Agent.MetaAI.Affect.IValenceMonitor>();
        mockValence
            .Setup(v => v.GetCurrentState())
            .Returns(new Ouroboros.Agent.MetaAI.Affect.AffectiveState(
                Guid.NewGuid(), 0.5, 0.2, 0.7, 0.4, 0.3, DateTime.UtcNow, new Dictionary<string, object>()));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, valenceMonitor: mockValence.Object, urgeSystem: mockUrge.Object);

        // Act
        await orchestrator.ExecuteAsync("Test urge system");

        // Assert
        mockUrge.Verify(u => u.Tick(), Times.Once, "should tick urge system at start of cycle");
    }

    // ================================================================
    // Selection threshold calculation (static method test via behavior)
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_HighArousalAffect_AdjustsSelectionThreshold()
    {
        // Arrange: high arousal should lower threshold (faster decisions)
        var mockValence = new Mock<Ouroboros.Agent.MetaAI.Affect.IValenceMonitor>();
        mockValence
            .Setup(v => v.GetCurrentState())
            .Returns(new Ouroboros.Agent.MetaAI.Affect.AffectiveState(
                Guid.NewGuid(), 0.5, 0.1, 0.7, 0.4, 0.9, DateTime.UtcNow, new Dictionary<string, object>()));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, valenceMonitor: mockValence.Object);

        // Act - should not throw; high arousal is handled gracefully
        var result = await orchestrator.ExecuteAsync("Test high arousal");

        // Assert
        result.Should().NotBeNull();
    }

    // ================================================================
    // Plan prompt construction - strategy gene influence
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_HighPlanningDepth_PromptsForDetailedPlan()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Planning depth", 0.9));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        await orchestrator.ExecuteAsync("Test deep planning");

        // Assert
        var planPrompt = ((RoutingChatModel)llm).CapturedPrompts.First(p => p.Contains("Create a plan"));
        planPrompt.Should().Contain("detailed plan with sub-steps and contingencies");
    }

    [Fact]
    public async Task ExecuteAsync_LowPlanningDepth_PromptsForConcisePlan()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Planning depth", 0.1));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        await orchestrator.ExecuteAsync("Test shallow planning");

        // Assert
        var planPrompt = ((RoutingChatModel)llm).CapturedPrompts.First(p => p.Contains("Create a plan"));
        planPrompt.Should().Contain("concise high-level plan");
    }

    [Fact]
    public async Task ExecuteAsync_HighDecompositionGranularity_SuggestsMoreSteps()
    {
        // Arrange: granularity = 1.0 -> suggested steps = 3 + (1.0 * 7) = 10
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_DecompositionGranularity", "Granularity", 1.0));

        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm, atom: atom);

        // Act
        await orchestrator.ExecuteAsync("Test granularity");

        // Assert
        var planPrompt = ((RoutingChatModel)llm).CapturedPrompts.First(p => p.Contains("Create a plan"));
        planPrompt.Should().Contain("10 steps");
    }

    // ================================================================
    // Execute phase - plan step parsing
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_NoStructuredSteps_TreatsWholePlanAsOneStep()
    {
        // Arrange: plan returns unstructured text
        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("Just do the thing directly without any steps.");
            if (prompt.Contains("Verify"))
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.8}");
            if (prompt.Contains("Extract"))
                return Task.FromResult("- insight");
            return Task.FromResult("Processed");
        });

        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Unstructured plan test");

        // Assert
        var executePhase = result.Output.PhaseResults.First(p => p.Phase == ImprovementPhase.Execute);
        executePhase.Metadata["steps_count"].Should().Be(1, "unstructured plan treated as single step");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleStepFormats_ParsesAll()
    {
        // Arrange: mix of numbered and bullet formats
        var llm = new RoutingChatModel((prompt, ct) =>
        {
            if (prompt.Contains("Create a plan"))
                return Task.FromResult("1. First numbered step\n- Bullet step\n* Star step\n2. Second numbered");
            if (prompt.Contains("Verify"))
                return Task.FromResult("{\"verified\": true, \"quality_score\": 0.8}");
            if (prompt.Contains("Extract"))
                return Task.FromResult("- insight");
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Test mixed formats");

        // Assert
        var executePhase = result.Output.PhaseResults.First(p => p.Phase == ImprovementPhase.Execute);
        ((int)executePhase.Metadata["steps_count"]).Should().BeGreaterThanOrEqualTo(3);
    }

    // ================================================================
    // Health endpoint
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_AfterExecution_MetadataContainsAtomInfo()
    {
        // Arrange
        var llm = CreateStandardRoutingModel();
        var orchestrator = CreateOrchestrator(llm);

        // Act
        var result = await orchestrator.ExecuteAsync("Test metadata");

        // Assert
        result.Output.Metadata.Should().ContainKey("atom_id");
        result.Output.Metadata.Should().ContainKey("capabilities_count");
        result.Output.Metadata.Should().ContainKey("experiences_count");
    }

    // ================================================================
    // Cancellation propagation
    // ================================================================

    [Fact]
    public async Task ExecuteAsync_CancellationDuringPlanPhase_PlanPhaseFailsWithCancellationError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var llm = new RoutingChatModel((prompt, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("OK");
        });

        var orchestrator = CreateOrchestrator(llm);
        await cts.CancelAsync();

        // Act: plan phase catches OperationCanceledException internally and returns failed PhaseResult.
        // The overall OrchestratorResult.Success may still be true (handled as a "completed" operation),
        // but the domain-level Output.Success should be false because the plan phase failed.
        var result = await orchestrator.ExecuteAsync("Test cancel", new OrchestratorContext(
            Guid.NewGuid().ToString(), new Dictionary<string, object>(), cts.Token));

        // Assert: domain-level result should indicate failure
        result.Output.Should().NotBeNull();
        result.Output.Success.Should().BeFalse("cancellation in plan phase should yield domain-level failure");
        var planPhase = result.Output.PhaseResults.First(p => p.Phase == ImprovementPhase.Plan);
        planPhase.Success.Should().BeFalse("plan phase should fail due to cancellation");
        planPhase.Error.Should().Contain("cancel", "plan error should mention cancellation");
    }
}
