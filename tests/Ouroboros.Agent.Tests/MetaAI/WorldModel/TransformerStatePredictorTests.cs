using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Xunit;
using WmAction = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Tests.MetaAI.WorldModel;

[Trait("Category", "Unit")]
public class TransformerStatePredictorTests
{
    [Fact]
    public void CreateRandom_WithValidParameters_CreatesPredictor()
    {
        var predictor = TransformerStatePredictor.CreateRandom(stateSize: 16, actionSize: 10, hiddenSize: 8, seed: 42);

        predictor.Should().NotBeNull();
    }

    [Fact]
    public async Task PredictAsync_ReturnsStateWithCorrectEmbeddingSize()
    {
        int stateSize = 16;
        var predictor = TransformerStatePredictor.CreateRandom(stateSize: stateSize, actionSize: 10, hiddenSize: 8, seed: 42);
        var state = new State(new Dictionary<string, object>(), new float[stateSize]);
        var action = new WmAction("move", new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state, action);

        result.Should().NotBeNull();
        result.Embedding.Should().HaveCount(stateSize);
    }

    [Fact]
    public async Task PredictAsync_WithSameSeed_ProducesDeterministicResults()
    {
        var p1 = TransformerStatePredictor.CreateRandom(8, 10, 16, seed: 99);
        var p2 = TransformerStatePredictor.CreateRandom(8, 10, 16, seed: 99);
        var state = new State(new Dictionary<string, object>(), new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f });
        var action = new WmAction("act", new Dictionary<string, object>());

        var r1 = await p1.PredictAsync(state, action);
        var r2 = await p2.PredictAsync(state, action);

        r1.Embedding.Should().BeEquivalentTo(r2.Embedding);
    }

    [Fact]
    public async Task PredictAsync_PreservesFeatures()
    {
        var predictor = TransformerStatePredictor.CreateRandom(4, 10, 8, seed: 42);
        var features = new Dictionary<string, object> { ["key"] = "value" };
        var state = new State(features, new float[] { 1f, 2f, 3f, 4f });
        var action = new WmAction("test", new Dictionary<string, object>());

        var result = await predictor.PredictAsync(state, action);

        result.Features.Should().ContainKey("key");
    }
}

[Trait("Category", "Unit")]
public class HybridStatePredictorTests
{
    [Fact]
    public async Task PredictAsync_CombinesBothPredictors()
    {
        int stateSize = 16;
        var transformer = TransformerStatePredictor.CreateRandom(stateSize, 10, 8, seed: 42);
        var gnn = GnnStatePredictor.CreateRandom(stateSize, 10, 8, seed: 42);
        var hybrid = new HybridStatePredictor(transformer, gnn);
        var state = new State(new Dictionary<string, object>(), new float[stateSize]);
        var action = new WmAction("test", new Dictionary<string, object>());

        var result = await hybrid.PredictAsync(state, action);

        result.Should().NotBeNull();
        result.Embedding.Should().HaveCount(stateSize);
    }

    [Fact]
    public void Constructor_WithNullTransformer_ThrowsArgumentNullException()
    {
        var gnn = GnnStatePredictor.CreateRandom(8, 4, 8, seed: 42);

        var act = () => new HybridStatePredictor(null!, gnn);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullGnn_ThrowsArgumentNullException()
    {
        var transformer = TransformerStatePredictor.CreateRandom(8, 10, 8, seed: 42);

        var act = () => new HybridStatePredictor(transformer, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task PredictAsync_AveragesEmbeddings()
    {
        int stateSize = 4;
        var transformer = TransformerStatePredictor.CreateRandom(stateSize, 10, 8, seed: 1);
        var gnn = GnnStatePredictor.CreateRandom(stateSize, 10, 8, seed: 2);
        var hybrid = new HybridStatePredictor(transformer, gnn);
        var state = new State(new Dictionary<string, object>(), new float[] { 1f, 2f, 3f, 4f });
        var action = new WmAction("test", new Dictionary<string, object>());

        var tResult = await transformer.PredictAsync(state, action);
        var gResult = await gnn.PredictAsync(state, action);
        var hResult = await hybrid.PredictAsync(state, action);

        // Each element should be the average of the two predictors
        for (int i = 0; i < stateSize; i++)
        {
            float expected = (tResult.Embedding[i] + gResult.Embedding[i]) * 0.5f;
            hResult.Embedding[i].Should().BeApproximately(expected, 0.001f);
        }
    }
}
