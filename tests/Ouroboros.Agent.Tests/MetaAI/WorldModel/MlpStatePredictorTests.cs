using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Xunit;
using WmAction = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Tests.MetaAI.WorldModel;

[Trait("Category", "Unit")]
public class MlpStatePredictorTests
{
    [Fact]
    public void CreateRandom_WithValidParameters_CreatesPredictor()
    {
        var predictor = MlpStatePredictor.CreateRandom(stateSize: 8, actionSize: 4, hiddenSize: 16, seed: 42);

        predictor.Should().NotBeNull();
    }

    [Fact]
    public async Task PredictAsync_ReturnsStateWithCorrectEmbeddingSize()
    {
        int stateSize = 8;
        var predictor = MlpStatePredictor.CreateRandom(stateSize: stateSize, actionSize: 4, hiddenSize: 16, seed: 42);
        var state = new State(new Dictionary<string, object>(), new float[stateSize]);
        var action = new WmAction("move", new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state, action);

        result.Should().NotBeNull();
        result.Embedding.Should().HaveCount(stateSize);
    }

    [Fact]
    public async Task PredictAsync_WithSameSeed_ProducesDeterministicResults()
    {
        var p1 = MlpStatePredictor.CreateRandom(4, 2, 8, seed: 99);
        var p2 = MlpStatePredictor.CreateRandom(4, 2, 8, seed: 99);
        var state = new State(new Dictionary<string, object>(), new float[] { 0.1f, 0.2f, 0.3f, 0.4f });
        var action = new WmAction("act", new Dictionary<string, object>());

        var r1 = await p1.PredictAsync(state, action);
        var r2 = await p2.PredictAsync(state, action);

        r1.Embedding.Should().BeEquivalentTo(r2.Embedding);
    }
}

[Trait("Category", "Unit")]
public class GnnStatePredictorTests
{
    [Fact]
    public void CreateRandom_WithValidParameters_CreatesPredictor()
    {
        var predictor = GnnStatePredictor.CreateRandom(stateSize: 16, actionSize: 4, hiddenSize: 8, seed: 42);

        predictor.Should().NotBeNull();
    }

    [Fact]
    public async Task PredictAsync_ReturnsStateWithCorrectEmbeddingSize()
    {
        int stateSize = 16;
        var predictor = GnnStatePredictor.CreateRandom(stateSize: stateSize, actionSize: 4, hiddenSize: 8, seed: 42);
        var state = new State(new Dictionary<string, object>(), new float[stateSize]);
        var action = new WmAction("interact", new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state, action);

        result.Should().NotBeNull();
        result.Embedding.Should().HaveCount(stateSize);
    }
}
