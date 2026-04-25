using Ouroboros.Agent.MetaAI.WorldModel;

namespace Ouroboros.Agent.Tests.WorldModel;

[Trait("Category", "Unit")]
public class WorldModelRecordsTests
{
    #region Action

    [Fact]
    public void Action_Creation_ShouldSetProperties()
    {
        var parameters = new Dictionary<string, object> { ["param1"] = "value1" };
        var action = new Action("Move", parameters);

        action.Name.Should().Be("Move");
        action.Parameters.Should().BeEquivalentTo(parameters);
    }

    [Fact]
    public void Action_Equality_SameValues_ShouldBeEqual()
    {
        var a = new Action("Move", new Dictionary<string, object> { ["p"] = 1 });
        var b = new Action("Move", new Dictionary<string, object> { ["p"] = 1 });

        a.Should().Be(b);
    }

    #endregion

    #region GnnStatePredictor

    [Fact]
    public void GnnStatePredictor_CreateRandom_ShouldReturnPredictor()
    {
        var predictor = GnnStatePredictor.CreateRandom(64, 4, 16, 2);
        predictor.Should().NotBeNull();
    }

    [Fact]
    public void GnnStatePredictor_PredictAsync_ShouldReturnState()
    {
        var predictor = GnnStatePredictor.CreateRandom(8, 2, 4, 1);
        var current = new State(new Dictionary<string, object> { ["x"] = 0.5f }, new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f });
        var action = new Action("test", new Dictionary<string, object>());

        var result = predictor.PredictAsync(current, action).Result;

        result.Should().NotBeNull();
        result.Features.Should().NotBeNull();
        result.Embedding.Should().NotBeNull();
    }

    #endregion

    #region HybridStatePredictor

    [Fact]
    public void HybridStatePredictor_Constructor_NullTransformer_ShouldThrow()
    {
        var gnn = GnnStatePredictor.CreateRandom(8, 2, 4, 1);
        Action act = () => new HybridStatePredictor(null!, gnn);
        act.Should().Throw<ArgumentNullException>().WithParameterName("transformer");
    }

    [Fact]
    public void HybridStatePredictor_Constructor_NullGnn_ShouldThrow()
    {
        var transformer = new TransformerStatePredictor(8, 2, 1, 2, new float[][] { new float[] { 0.1f } }, new float[] { 0.0f }, new float[][] { new float[] { 0.1f } }, new float[] { 0.0f }, new float[][] { new float[] { 0.1f } }, new float[] { 0.0f }, new float[] { 0.0f });
        Action act = () => new HybridStatePredictor(transformer, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("gnn");
    }

    [Fact]
    public async Task HybridStatePredictor_PredictAsync_ShouldReturnAveragedState()
    {
        var transformer = new TransformerStatePredictor(8, 2, 1, 2, new float[][] { new float[] { 0.1f } }, new float[] { 0.0f }, new float[][] { new float[] { 0.1f } }, new float[] { 0.0f }, new float[][] { new float[] { 0.1f } }, new float[] { 0.0f }, new float[] { 0.0f });
        var gnn = GnnStatePredictor.CreateRandom(8, 2, 4, 1);
        var hybrid = new HybridStatePredictor(transformer, gnn);
        var current = new State(new Dictionary<string, object> { ["x"] = 0.5f }, new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f });
        var action = new Action("test", new Dictionary<string, object>());

        var result = await hybrid.PredictAsync(current, action);

        result.Should().NotBeNull();
        result.Embedding.Should().NotBeNull();
    }

    #endregion

    #region MlpStatePredictor

    [Fact]
    public void MlpStatePredictor_Constructor_ValidArgs_ShouldInitialize()
    {
        var w1 = new float[][] { new float[] { 0.1f, 0.2f }, new float[] { 0.3f, 0.4f } };
        var b1 = new float[] { 0.0f, 0.0f };
        var w2 = new float[][] { new float[] { 0.1f, 0.2f } };
        var b2 = new float[] { 0.0f };

        var predictor = new MlpStatePredictor(2, 2, 1, w1, b1, w2, b2);
        predictor.Should().NotBeNull();
    }

    [Fact]
    public async Task MlpStatePredictor_PredictAsync_ShouldReturnState()
    {
        var w1 = new float[][] { new float[] { 0.1f, 0.2f }, new float[] { 0.3f, 0.4f } };
        var b1 = new float[] { 0.0f, 0.0f };
        var w2 = new float[][] { new float[] { 0.1f, 0.2f } };
        var b2 = new float[] { 0.0f };

        var predictor = new MlpStatePredictor(2, 2, 1, w1, b1, w2, b2);
        var current = new State(new Dictionary<string, object> { ["x"] = 0.5f }, new float[] { 0.1f, 0.2f });
        var action = new Action("test", new Dictionary<string, object>());

        var result = await predictor.PredictAsync(current, action);

        result.Should().NotBeNull();
        result.Embedding.Should().NotBeNull();
        result.Embedding.Length.Should().Be(1);
    }

    #endregion
}
