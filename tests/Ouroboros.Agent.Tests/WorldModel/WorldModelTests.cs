// <copyright file="WorldModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Embodied;
using AgentWorldModel = Ouroboros.Agent.WorldModel.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public class WorldModelTests
{
    [Fact]
    public void Constructor_InvalidMaxMemory_Throws()
    {
        var act = () => new AgentWorldModel(maxMemorySize: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_InvalidKNeighbors_Throws()
    {
        var act = () => new AgentWorldModel(kNeighbors: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => new AgentWorldModel(100, 3);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task PredictAsync_NullState_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var action = EmbodiedAction.NoOp();

        var result = await wm.PredictAsync(null!, action);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PredictAsync_NullAction_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var state = CreateSensorState();

        var result = await wm.PredictAsync(state, null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PredictAsync_NoExperience_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var state = CreateSensorState();
        var action = EmbodiedAction.NoOp();

        var result = await wm.PredictAsync(state, action);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No experience");
    }

    [Fact]
    public async Task UpdateFromExperienceAsync_NullTransitions_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.UpdateFromExperienceAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFromExperienceAsync_EmptyTransitions_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.UpdateFromExperienceAsync(new List<EmbodiedTransition>());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFromExperienceAsync_ValidTransitions_Succeeds()
    {
        var wm = new AgentWorldModel();
        var transitions = new List<EmbodiedTransition>
        {
            CreateTransition()
        };

        var result = await wm.UpdateFromExperienceAsync(transitions);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUncertaintyAsync_NullState_ReturnsMax()
    {
        var wm = new AgentWorldModel();
        var uncertainty = await wm.GetUncertaintyAsync(null!, EmbodiedAction.NoOp());
        uncertainty.Should().Be(1.0);
    }

    [Fact]
    public async Task GetUncertaintyAsync_NoExperience_ReturnsMax()
    {
        var wm = new AgentWorldModel();
        var state = CreateSensorState();
        var uncertainty = await wm.GetUncertaintyAsync(state, EmbodiedAction.NoOp());
        uncertainty.Should().Be(1.0);
    }

    [Fact]
    public async Task PlanWithModelAsync_NullState_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.PlanWithModelAsync(null!, "goal");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanWithModelAsync_EmptyGoal_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.PlanWithModelAsync(CreateSensorState(), "  ");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanWithModelAsync_InvalidHorizon_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.PlanWithModelAsync(CreateSensorState(), "goal", horizon: 0);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_NullState_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.SimulateTrajectoryAsync(null!, new List<EmbodiedAction> { EmbodiedAction.NoOp() });
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_EmptyActions_ReturnsFailure()
    {
        var wm = new AgentWorldModel();
        var result = await wm.SimulateTrajectoryAsync(CreateSensorState(), new List<EmbodiedAction>());
        result.IsFailure.Should().BeTrue();
    }

    private static SensorState CreateSensorState()
    {
        return new SensorState(
            new Vector3(0, 0, 0),
            Quaternion.Identity,
            new Vector3(0, 0, 0),
            Array.Empty<float>(),
            Array.Empty<float>(),
            new Dictionary<string, float>(),
            DateTime.UtcNow);
    }

    private static EmbodiedTransition CreateTransition()
    {
        var state1 = CreateSensorState();
        var state2 = new SensorState(
            new Vector3(1, 0, 0),
            Quaternion.Identity,
            new Vector3(1, 0, 0),
            Array.Empty<float>(),
            Array.Empty<float>(),
            new Dictionary<string, float>(),
            DateTime.UtcNow);

        return new EmbodiedTransition(
            state1,
            EmbodiedAction.Move(Vector3.UnitX, "Forward"),
            state2,
            1.0,
            false);
    }
}
