// <copyright file="WorldModelEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Comprehensive unit tests for <see cref="WorldModelEngine"/>.
/// Tests cover all public methods including the local-type API and the
/// Abstractions-level <see cref="IWorldModelEngine"/> interface bridge.
/// </summary>
[Trait("Category", "Unit")]
public class WorldModelEngineTests
{
    private const int EmbeddingSize = 8;
    private const int DefaultSeed = 42;

    // ------------------------------------------------------------------
    // Helper methods
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="State"/> with the given features and a random embedding.
    /// </summary>
    private static State CreateTestState(
        Dictionary<string, object>? features = null,
        int embeddingSize = EmbeddingSize,
        int seed = 0)
    {
        var rng = new Random(seed);
        var embedding = Enumerable.Range(0, embeddingSize)
            .Select(_ => (float)(rng.NextDouble() * 2 - 1))
            .ToArray();
        return new State(
            features ?? new Dictionary<string, object> { ["x"] = 1.0, ["y"] = 2.0 },
            embedding);
    }

    /// <summary>
    /// Creates a simple <see cref="Action"/> for testing.
    /// </summary>
    private static Action CreateTestAction(string name = "move_forward")
    {
        return new Action(name, new Dictionary<string, object> { ["speed"] = 1.0 });
    }

    /// <summary>
    /// Creates a list of consistent <see cref="Transition"/> records for training/evaluation.
    /// </summary>
    private static List<Transition> CreateTestTransitions(
        int count = 10,
        int embeddingSize = EmbeddingSize,
        bool terminal = false)
    {
        var transitions = new List<Transition>();
        var rng = new Random(DefaultSeed);

        for (int i = 0; i < count; i++)
        {
            var prevState = CreateTestState(
                new Dictionary<string, object> { ["step"] = i },
                embeddingSize,
                seed: i);
            var nextState = CreateTestState(
                new Dictionary<string, object> { ["step"] = i + 1 },
                embeddingSize,
                seed: i + 100);
            var action = CreateTestAction($"action_{i % 3}");

            transitions.Add(new Transition(
                PreviousState: prevState,
                ActionTaken: action,
                NextState: nextState,
                Reward: rng.NextDouble(),
                Terminal: terminal && i == count - 1));
        }

        return transitions;
    }

    /// <summary>
    /// Creates a <see cref="WorldState"/> for the Abstractions-level interface tests.
    /// </summary>
    private static WorldState CreateTestWorldState(
        Dictionary<string, object>? features = null,
        int embeddingSize = EmbeddingSize,
        int seed = 0)
    {
        var rng = new Random(seed);
        var embedding = Enumerable.Range(0, embeddingSize)
            .Select(_ => (float)(rng.NextDouble() * 2 - 1))
            .ToArray();

        var feats = features ?? new Dictionary<string, object> { ["x"] = 1.0, ["y"] = 2.0 };
        feats["embedding"] = embedding;

        return new WorldState(Guid.NewGuid(), feats, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a list of <see cref="WorldTransition"/> records for the Abstractions-level interface tests.
    /// </summary>
    private static List<WorldTransition> CreateTestWorldTransitions(
        int count = 10,
        int embeddingSize = EmbeddingSize)
    {
        var transitions = new List<WorldTransition>();
        var rng = new Random(DefaultSeed);

        for (int i = 0; i < count; i++)
        {
            var fromState = CreateTestWorldState(
                new Dictionary<string, object> { ["step"] = i },
                embeddingSize,
                seed: i);
            var toState = CreateTestWorldState(
                new Dictionary<string, object> { ["step"] = i + 1 },
                embeddingSize,
                seed: i + 100);
            var action = new AgentAction($"action_{i % 3}", new Dictionary<string, object> { ["speed"] = 1.0 });

            transitions.Add(new WorldTransition(fromState, action, toState, rng.NextDouble()));
        }

        return transitions;
    }

    /// <summary>
    /// Convenience helper: learns a model using the local API and returns it.
    /// </summary>
    private static async Task<Ouroboros.Agent.MetaAI.WorldModel.WorldModel> LearnDefaultModelAsync(
        WorldModelEngine engine,
        ModelArchitecture architecture = ModelArchitecture.MLP)
    {
        var transitions = CreateTestTransitions();
        var result = await engine.LearnModelAsync(transitions, architecture, CancellationToken.None);
        result.IsSuccess.Should().BeTrue("helper requires a successfully learned model");
        return result.Value;
    }

    // ------------------------------------------------------------------
    // LearnModelAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task LearnModelAsync_WithValidTransitions_ReturnsWorldModel()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var transitions = CreateTestTransitions();

        // Act
        var result = await engine.LearnModelAsync(transitions, ModelArchitecture.MLP, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var model = result.Value;
        model.Id.Should().NotBeEmpty();
        model.Domain.Should().Be("learned");
        model.TransitionModel.Should().NotBeNull();
        model.RewardModel.Should().NotBeNull();
        model.TerminalModel.Should().NotBeNull();
        model.Hyperparameters.Should().ContainKey("architecture");
        model.Hyperparameters["architecture"].Should().Be("MLP");
        model.Hyperparameters.Should().ContainKey("training_samples");
        model.Hyperparameters["training_samples"].Should().Be(transitions.Count);
    }

    [Theory]
    [InlineData(ModelArchitecture.MLP)]
    [InlineData(ModelArchitecture.Transformer)]
    [InlineData(ModelArchitecture.GNN)]
    [InlineData(ModelArchitecture.Hybrid)]
    public async Task LearnModelAsync_AllArchitectures_Succeed(ModelArchitecture architecture)
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var transitions = CreateTestTransitions();

        // Act
        var result = await engine.LearnModelAsync(transitions, architecture, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var model = result.Value;
        model.Hyperparameters["architecture"].Should().Be(architecture.ToString());
    }

    [Fact]
    public async Task LearnModelAsync_WithEmptyTransitions_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var transitions = new List<Transition>();

        // Act
        var result = await engine.LearnModelAsync(transitions, ModelArchitecture.MLP, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task LearnModelAsync_WithNullTransitions_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);

        // Act
        var result = await engine.LearnModelAsync(null!, ModelArchitecture.MLP, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task LearnModelAsync_WithInconsistentEmbeddings_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);

        // Create transitions with mismatched embedding sizes
        var state8 = CreateTestState(embeddingSize: 8);
        var state4 = CreateTestState(embeddingSize: 4, seed: 1);
        var action = CreateTestAction();

        var transitions = new List<Transition>
        {
            new(state8, action, state8, 1.0, false),
            new(state4, action, state4, 1.0, false),
        };

        // Act
        var result = await engine.LearnModelAsync(transitions, ModelArchitecture.MLP, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Inconsistent embedding sizes");
    }

    // ------------------------------------------------------------------
    // PredictNextStateAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task PredictNextStateAsync_WithValidInputs_ReturnsPredictedState()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var state = CreateTestState();
        var action = CreateTestAction();

        // Act
        var result = await WorldModelEngine.PredictNextStateAsync(state, action, model, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var predictedState = result.Value;
        predictedState.Should().NotBeNull();
        predictedState.Embedding.Should().NotBeNull();
        predictedState.Embedding.Length.Should().Be(EmbeddingSize);
        predictedState.Features.Should().NotBeNull();
    }

    [Fact]
    public async Task PredictNextStateAsync_WithNullState_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var action = CreateTestAction();

        // Act
        var result = await WorldModelEngine.PredictNextStateAsync(null!, action, model, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("state");
    }

    [Fact]
    public async Task PredictNextStateAsync_WithNullAction_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var state = CreateTestState();

        // Act
        var result = await WorldModelEngine.PredictNextStateAsync(state, null!, model, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().ContainEquivalentOf("action");
    }

    [Fact]
    public async Task PredictNextStateAsync_WithNullModel_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var state = CreateTestState();
        var action = CreateTestAction();

        // Act
        var result = await WorldModelEngine.PredictNextStateAsync(state, action, null!, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Model");
    }

    // ------------------------------------------------------------------
    // PlanInImaginationAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task PlanInImaginationAsync_WithValidModel_ReturnsPlan()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var initialState = CreateTestState();
        var goal = "reach the target location";
        int lookaheadDepth = 3;

        // Act
        var result = await engine.PlanInImaginationAsync(
            initialState, goal, model, lookaheadDepth, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;
        plan.Description.Should().Contain(goal);
        plan.Actions.Should().NotBeEmpty();
        plan.Actions.Count.Should().BeLessThanOrEqualTo(lookaheadDepth);
        plan.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task PlanInImaginationAsync_WithEmptyGoal_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var initialState = CreateTestState();

        // Act
        var result = await engine.PlanInImaginationAsync(
            initialState, string.Empty, model, 3, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal");
    }

    [Fact]
    public async Task PlanInImaginationAsync_WithZeroDepth_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var initialState = CreateTestState();

        // Act
        var result = await engine.PlanInImaginationAsync(
            initialState, "some goal", model, 0, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("depth");
    }

    // ------------------------------------------------------------------
    // EvaluateModelAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task EvaluateModelAsync_WithTestSet_ReturnsQualityMetrics()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var testSet = CreateTestTransitions(count: 5);

        // Act
        var result = await WorldModelEngine.EvaluateModelAsync(model, testSet, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var quality = result.Value;
        quality.PredictionAccuracy.Should().BeInRange(0.0, 1.0);
        quality.RewardCorrelation.Should().BeInRange(0.0, 1.0);
        quality.TerminalAccuracy.Should().BeInRange(0.0, 1.0);
        quality.CalibrationError.Should().BeGreaterThanOrEqualTo(0.0);
        quality.TestSamples.Should().Be(5);
    }

    [Fact]
    public async Task EvaluateModelAsync_WithEmptyTestSet_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var emptyTestSet = new List<Transition>();

        // Act
        var result = await WorldModelEngine.EvaluateModelAsync(model, emptyTestSet, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    // ------------------------------------------------------------------
    // GenerateSyntheticExperienceAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithValidModel_ReturnsTransitions()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var startState = CreateTestState();
        int trajectoryLength = 5;

        // Act
        var result = await engine.GenerateSyntheticExperienceAsync(
            model, startState, trajectoryLength, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transitions = result.Value;
        transitions.Should().NotBeEmpty();
        transitions.Count.Should().BeLessThanOrEqualTo(trajectoryLength);

        // Verify each transition has valid structure
        foreach (var transition in transitions)
        {
            transition.PreviousState.Should().NotBeNull();
            transition.ActionTaken.Should().NotBeNull();
            transition.NextState.Should().NotBeNull();
            transition.ActionTaken.Name.Should().NotBeNullOrEmpty();
        }

        // Verify sequential state chaining (next state of transition i == previous state of transition i+1)
        for (int i = 0; i < transitions.Count - 1; i++)
        {
            if (!transitions[i].Terminal)
            {
                transitions[i + 1].PreviousState.Should().Be(transitions[i].NextState);
            }
        }
    }

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithZeroLength_ReturnsFailure()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        var model = await LearnDefaultModelAsync(engine);
        var startState = CreateTestState();

        // Act
        var result = await engine.GenerateSyntheticExperienceAsync(
            model, startState, 0, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("length");
    }

    // ------------------------------------------------------------------
    // IWorldModelEngine interface bridge tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task InterfaceBridge_LearnModelAsync_WorksThroughAbstractionsInterface()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        IWorldModelEngine bridgeEngine = engine;
        var worldTransitions = CreateTestWorldTransitions();

        // Act
        var result = await bridgeEngine.LearnModelAsync(
            worldTransitions, ModelArchitecture.MLP, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var learnedModel = result.Value;
        learnedModel.Id.Should().NotBeEmpty();
        learnedModel.Name.Should().Be("learned");
        learnedModel.Architecture.Should().Be(ModelArchitecture.MLP);
        learnedModel.TrainingSamples.Should().Be(worldTransitions.Count);
        learnedModel.TrainedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InterfaceBridge_PredictNextStateAsync_WorksThroughAbstractionsInterface()
    {
        // Arrange
        var engine = new WorldModelEngine(DefaultSeed);
        IWorldModelEngine bridgeEngine = engine;

        // First learn a model through the bridge so it's registered in the internal dictionary
        var worldTransitions = CreateTestWorldTransitions();
        var learnResult = await bridgeEngine.LearnModelAsync(
            worldTransitions, ModelArchitecture.MLP, CancellationToken.None);
        learnResult.IsSuccess.Should().BeTrue("precondition: model must be learned successfully");
        var learnedModel = learnResult.Value;

        var currentState = CreateTestWorldState();
        var action = new AgentAction("move_forward", new Dictionary<string, object> { ["speed"] = 1.0 });

        // Act
        var result = await bridgeEngine.PredictNextStateAsync(
            currentState, action, learnedModel, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var predictedWorldState = result.Value;
        predictedWorldState.Id.Should().NotBeEmpty();
        predictedWorldState.Features.Should().NotBeNull();
        predictedWorldState.Features.Should().ContainKey("embedding");
        predictedWorldState.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
