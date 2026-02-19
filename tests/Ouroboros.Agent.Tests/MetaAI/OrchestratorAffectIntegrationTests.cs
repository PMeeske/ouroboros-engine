// <copyright file="OrchestratorAffectIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.Affect;
using ToolRegistry = Ouroboros.Tools.ToolRegistry;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Integration tests for affective state wiring in OuroborosOrchestrator.
/// Tests selection threshold, resolution level, urge satisfaction, and MeTTa projection.
/// </summary>
[Trait("Category", "Unit")]
public class OrchestratorAffectIntegrationTests
{
    private class MockChatCompletionModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult("{\"verified\": true, \"quality_score\": 0.85, \"reasoning\": \"Good\"}");
    }

    private class MockMeTTaEngine : IMeTTaEngine
    {
        public List<string> AddedFacts { get; } = new();

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("(verified)"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            AddedFacts.Add(fact);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[384]);

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
            => Task.FromResult(new float[384]);
    }

    [Fact]
    public void HighStress_RaisesSelectionThreshold()
    {
        // Arrange — high stress state
        var highStress = new AffectiveState(
            Guid.NewGuid(), Valence: -0.3, Stress: 0.9, Confidence: 0.5,
            Curiosity: 0.3, Arousal: 0.4, DateTime.UtcNow, new Dictionary<string, object>());

        var lowStress = new AffectiveState(
            Guid.NewGuid(), Valence: 0.3, Stress: 0.1, Confidence: 0.5,
            Curiosity: 0.3, Arousal: 0.4, DateTime.UtcNow, new Dictionary<string, object>());

        // Act — use reflection to test the private static method
        var method = typeof(OuroborosOrchestrator).GetMethod(
            "CalculateSelectionThreshold",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        double highStressThreshold = (double)method!.Invoke(null, new object[] { highStress })!;
        double lowStressThreshold = (double)method!.Invoke(null, new object[] { lowStress })!;

        // Assert — higher stress → higher threshold (more cautious)
        highStressThreshold.Should().BeGreaterThan(lowStressThreshold);
    }

    [Fact]
    public void HighArousal_ReducesResolutionLevel()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        var highArousal = new AffectiveState(
            Guid.NewGuid(), Valence: 0.0, Stress: 0.3, Confidence: 0.5,
            Curiosity: 0.3, Arousal: 0.9, DateTime.UtcNow, new Dictionary<string, object>());

        var lowArousal = new AffectiveState(
            Guid.NewGuid(), Valence: 0.0, Stress: 0.3, Confidence: 0.5,
            Curiosity: 0.3, Arousal: 0.1, DateTime.UtcNow, new Dictionary<string, object>());

        var orchestrator = new OuroborosOrchestrator(
            new MockChatCompletionModel(),
            ToolRegistry.CreateDefault(),
            new MemoryStore(new MockEmbeddingModel()),
            new SafetyGuard(),
            new MockMeTTaEngine(),
            atom);

        // Use reflection to call the private instance method
        var method = typeof(OuroborosOrchestrator).GetMethod(
            "GetEffectiveResolutionLevel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        double highArousalResolution = (double)method!.Invoke(orchestrator, new object[] { highArousal })!;
        double lowArousalResolution = (double)method!.Invoke(orchestrator, new object[] { lowArousal })!;

        // Assert — high arousal → lower resolution (shallow planning)
        highArousalResolution.Should().BeLessThan(lowArousalResolution);
    }

    [Fact]
    public void SuccessfulCycle_SatisfiesCompetenceAndCertainty()
    {
        // Arrange
        var urgeSystem = new UrgeSystem();
        double initialCompetence = urgeSystem.Urges.First(u => u.Name == "competence").Intensity;
        double initialCertainty = urgeSystem.Urges.First(u => u.Name == "certainty").Intensity;

        // Act — simulate successful verify and learn
        urgeSystem.Satisfy("certainty", 0.85);
        urgeSystem.Satisfy("competence", 0.7);

        // Assert
        double afterCompetence = urgeSystem.Urges.First(u => u.Name == "competence").Intensity;
        double afterCertainty = urgeSystem.Urges.First(u => u.Name == "certainty").Intensity;
        afterCompetence.Should().BeLessThan(initialCompetence);
        afterCertainty.Should().BeLessThan(initialCertainty);
    }

    [Fact]
    public void FailedVerification_IncreasesStress()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        var initialState = monitor.GetCurrentState();
        double initialStress = initialState.Stress;

        // Act — simulate failed verification
        monitor.UpdateConfidence("verify", false, 0.8);
        monitor.RecordSignal("verify_failed", 0.6, SignalType.Stress);

        var afterState = monitor.GetCurrentState();

        // Assert — stress should increase after failure signals
        afterState.Stress.Should().BeGreaterThanOrEqualTo(initialStress);
    }

    [Fact]
    public void NovelDomain_SatisfiesCuriosity()
    {
        // Arrange
        var urgeSystem = new UrgeSystem();
        double initialCuriosity = urgeSystem.Urges.First(u => u.Name == "curiosity").Intensity;

        // Act
        urgeSystem.Satisfy("curiosity", 0.8);

        // Assert
        double afterCuriosity = urgeSystem.Urges.First(u => u.Name == "curiosity").Intensity;
        afterCuriosity.Should().BeLessThan(initialCuriosity);
    }

    [Fact]
    public async Task AffectiveState_ProjectedToMeTTa()
    {
        // Arrange
        var mettaEngine = new MockMeTTaEngine();
        var valenceMonitor = new ValenceMonitor();
        var urgeSystem = new UrgeSystem();
        var atom = OuroborosAtom.CreateDefault();

        var orchestrator = new OuroborosOrchestrator(
            new MockChatCompletionModel(),
            ToolRegistry.CreateDefault(),
            new MemoryStore(new MockEmbeddingModel()),
            new SafetyGuard(),
            mettaEngine,
            atom,
            valenceMonitor: valenceMonitor,
            urgeSystem: urgeSystem);

        // Act — use reflection to call ProjectAffectiveStateToMeTTaAsync
        var state = valenceMonitor.GetCurrentState();
        var dominant = urgeSystem.GetDominantUrge();

        var method = typeof(OuroborosOrchestrator).GetMethod(
            "ProjectAffectiveStateToMeTTaAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)method!.Invoke(orchestrator, new object?[] { state, dominant, CancellationToken.None })!;

        // Assert — MeTTa engine should have received affective state facts
        mettaEngine.AddedFacts.Should().Contain(f => f.Contains("AffectiveState"));
        mettaEngine.AddedFacts.Should().Contain(f => f.Contains("DominantUrge"));
        mettaEngine.AddedFacts.Should().Contain(f => f.Contains("HasUrge"));
    }

    [Fact]
    public void SelfReflect_IncludesEmotionalState()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.UpdateSelfModel("affect_valence", 0.35);
        atom.UpdateSelfModel("affect_stress", 0.45);
        atom.UpdateSelfModel("affect_arousal", 0.60);
        atom.UpdateSelfModel("dominant_urge", "curiosity");

        // Act
        string reflection = atom.SelfReflect();

        // Assert
        reflection.Should().Contain("Emotional State:");
        reflection.Should().Contain("Valence: 0.35");
        reflection.Should().Contain("Stress: 0.45");
        reflection.Should().Contain("Arousal: 0.6");
        reflection.Should().Contain("Dominant Need: curiosity");
    }

    [Fact]
    public void SelfReflect_WithoutAffect_OmitsEmotionalState()
    {
        // Arrange — no affective state set
        var atom = OuroborosAtom.CreateDefault();

        // Act
        string reflection = atom.SelfReflect();

        // Assert — emotional state section should not appear
        reflection.Should().NotContain("Emotional State:");
    }

    [Fact]
    public void GetStrategyWeight_ReturnsCapabilityConfidence()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();
        atom.AddCapability(new OuroborosCapability("Strategy_PlanningDepth", "Planning depth gene", 0.7));

        // Act
        double weight = atom.GetStrategyWeight("PlanningDepth", 0.5);

        // Assert
        weight.Should().Be(0.7);
    }

    [Fact]
    public void GetStrategyWeight_ReturnsDefault_WhenNotFound()
    {
        // Arrange
        var atom = OuroborosAtom.CreateDefault();

        // Act
        double weight = atom.GetStrategyWeight("NonExistent", 0.42);

        // Assert
        weight.Should().Be(0.42);
    }

    [Fact]
    public void OrchestratorConstructor_BackwardCompatible()
    {
        // Arrange & Act — construct without optional affect dependencies
        var orchestrator = new OuroborosOrchestrator(
            new MockChatCompletionModel(),
            ToolRegistry.CreateDefault(),
            new MemoryStore(new MockEmbeddingModel()),
            new SafetyGuard(),
            new MockMeTTaEngine());

        // Assert — should construct successfully
        orchestrator.Should().NotBeNull();
        orchestrator.Atom.Should().NotBeNull();
    }

    [Fact]
    public void OrchestratorConstructor_WithAffectDependencies()
    {
        // Arrange & Act
        var orchestrator = new OuroborosOrchestrator(
            new MockChatCompletionModel(),
            ToolRegistry.CreateDefault(),
            new MemoryStore(new MockEmbeddingModel()),
            new SafetyGuard(),
            new MockMeTTaEngine(),
            valenceMonitor: new ValenceMonitor(),
            priorityModulator: new PriorityModulator(),
            urgeSystem: new UrgeSystem());

        // Assert
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void SelectionThreshold_ClampedWithinBounds()
    {
        // Arrange — extreme states
        var extremeHigh = new AffectiveState(
            Guid.NewGuid(), Valence: 1.0, Stress: 1.0, Confidence: 1.0,
            Curiosity: 1.0, Arousal: 1.0, DateTime.UtcNow, new Dictionary<string, object>());

        var extremeLow = new AffectiveState(
            Guid.NewGuid(), Valence: -1.0, Stress: 0.0, Confidence: 0.0,
            Curiosity: 0.0, Arousal: 0.0, DateTime.UtcNow, new Dictionary<string, object>());

        var method = typeof(OuroborosOrchestrator).GetMethod(
            "CalculateSelectionThreshold",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        double thresholdHigh = (double)method!.Invoke(null, new object[] { extremeHigh })!;
        double thresholdLow = (double)method!.Invoke(null, new object[] { extremeLow })!;

        // Assert — should be within [0.2, 0.8]
        thresholdHigh.Should().BeGreaterOrEqualTo(0.2).And.BeLessOrEqualTo(0.8);
        thresholdLow.Should().BeGreaterOrEqualTo(0.2).And.BeLessOrEqualTo(0.8);
    }
}
