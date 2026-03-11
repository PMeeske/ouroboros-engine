using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Xunit;
using WmAction = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Tests.MetaAI.WorldModel;

[Trait("Category", "Unit")]
public class SimpleRewardPredictorTests
{
    [Fact]
    public async Task PredictAsync_WithZeroWeightsAndZeroBias_ReturnsZero()
    {
        var predictor = new SimpleRewardPredictor(new float[] { 0, 0, 0 }, 0.0f);
        var state = new State("s1", new float[] { 0.5f, 0.5f, 0.5f }, new Dictionary<string, object>());
        var action = new WmAction("act", new Dictionary<string, object>());
        var nextState = new State("s2", new float[] { 0.6f, 0.6f, 0.6f }, new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state, action, nextState);

        result.Should().Be(0.0);
    }

    [Fact]
    public async Task PredictAsync_WithNonZeroWeights_ReturnsNonZero()
    {
        var predictor = new SimpleRewardPredictor(new float[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f }, 0.5f);
        var state = new State("s1", new float[] { 0.1f, 0.2f, 0.3f }, new Dictionary<string, object>());
        var action = new WmAction("act", new Dictionary<string, object>());
        var nextState = new State("s2", new float[] { 0.4f, 0.5f, 0.6f }, new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state, action, nextState);

        result.Should().NotBe(0.0);
    }

    [Fact]
    public void CreateRandom_WithGivenSize_CreatesPredictor()
    {
        var predictor = SimpleRewardPredictor.CreateRandom(10, seed: 42);

        predictor.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRandom_ProducesDeterministicResults()
    {
        var predictor1 = SimpleRewardPredictor.CreateRandom(5, seed: 123);
        var predictor2 = SimpleRewardPredictor.CreateRandom(5, seed: 123);
        var state = new State("s", new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f }, new Dictionary<string, object>());
        var action = new WmAction("a", new Dictionary<string, object>());

        var r1 = await predictor1.PredictAsync(state, action, state);
        var r2 = await predictor2.PredictAsync(state, action, state);

        r1.Should().Be(r2);
    }
}

[Trait("Category", "Unit")]
public class SimpleTerminalPredictorTests
{
    [Fact]
    public async Task PredictAsync_WithHighBias_ReturnsTrue()
    {
        var predictor = new SimpleTerminalPredictor(new float[] { 0, 0 }, 10.0f);
        var state = new State("s", new float[] { 0, 0 }, new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task PredictAsync_WithVeryNegativeBias_ReturnsFalse()
    {
        var predictor = new SimpleTerminalPredictor(new float[] { 0, 0 }, -10.0f);
        var state = new State("s", new float[] { 0, 0 }, new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state);

        result.Should().BeFalse();
    }

    [Fact]
    public void CreateRandom_WithGivenSize_CreatesPredictor()
    {
        var predictor = SimpleTerminalPredictor.CreateRandom(8, seed: 42);

        predictor.Should().NotBeNull();
    }

    [Fact]
    public async Task PredictAsync_WithCustomThreshold_UsesThreshold()
    {
        var predictor = new SimpleTerminalPredictor(new float[] { 0 }, 0.0f, threshold: 0.5f);
        var state = new State("s", new float[] { 0 }, new Dictionary<string, object>());

        // sigmoid(0) = 0.5, threshold = 0.5 => true
        var result = await predictor.PredictAsync(state);

        result.Should().BeTrue();
    }
}
