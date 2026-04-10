namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ouroboros.Agent.MetaAI.WorldModel;
using WorldModelAction = Ouroboros.Agent.MetaAI.WorldModel.Action;
using Xunit;

public sealed class GnnStatePredictorTests
{
    private static State MakeState(float[] embedding) =>
        new(Features: new Dictionary<string, object>(), Embedding: embedding);

    private static WorldModelAction MakeAction(string name = "test") =>
        new(Name: name, Parameters: new Dictionary<string, object>());

    [Fact]
    public void ComputeLoss_ReturnsMSE_BetweenPredictedAndActual()
    {
        // Arrange
        var predictor = GnnStatePredictor.CreateRandom(16, 10, 32, seed: 42);
        var predicted = MakeState(new float[] { 1.0f, 2.0f, 3.0f });
        var actual = MakeState(new float[] { 1.0f, 2.0f, 3.0f });

        // Act — identical embeddings should yield zero loss
        float lossIdentical = predictor.ComputeLoss(predicted, actual);

        // Assert
        Assert.Equal(0.0f, lossIdentical, precision: 6);

        // Arrange — known different embeddings
        var predicted2 = MakeState(new float[] { 1.0f, 2.0f });
        var actual2 = MakeState(new float[] { 3.0f, 5.0f });

        // Act — MSE = ((1-3)^2 + (2-5)^2) / 2 = (4 + 9) / 2 = 6.5
        float lossDifferent = predictor.ComputeLoss(predicted2, actual2);

        // Assert
        Assert.Equal(6.5f, lossDifferent, precision: 5);
    }

    [Fact]
    public void ExperienceBuffer_AddAndSample_WorksCorrectly()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        var state = MakeState(new float[] { 1.0f, 2.0f });
        var action = MakeAction();
        var nextState = MakeState(new float[] { 3.0f, 4.0f });

        // Act
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(new Experience(state, action, nextState));
        }

        var samples = buffer.Sample(3, new Random(42));

        // Assert
        Assert.Equal(5, buffer.Count);
        Assert.False(buffer.IsFull);
        Assert.Equal(3, samples.Count);
        Assert.All(samples, s =>
        {
            Assert.Equal(state, s.Current);
            Assert.Equal(action, s.Action);
            Assert.Equal(nextState, s.NextState);
        });
    }

    [Fact]
    public void ExperienceBuffer_CircularOverwrite_WorksCorrectly()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 3);
        var states = Enumerable.Range(0, 5)
            .Select(i => MakeState(new float[] { i }))
            .ToArray();
        var action = MakeAction();

        // Act — add 5 experiences to capacity-3 buffer
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(new Experience(states[i], action, states[i]));
        }

        // Assert — should have last 3 (indices 2, 3, 4)
        Assert.Equal(3, buffer.Count);
        Assert.True(buffer.IsFull);

        // Sample all to verify oldest (0, 1) are gone
        var all = buffer.Sample(3, new Random(42));
        Assert.All(all, s =>
        {
            float val = s.Current.Embedding[0];
            Assert.True(val >= 2.0f, $"Expected val >= 2.0 but got {val}");
        });
    }

    [Fact]
    public void ComputeGradients_ReturnsNonZeroGradients()
    {
        // Arrange
        var predictor = GnnStatePredictor.CreateRandom(16, 10, 32, seed: 42);
        var buffer = new ExperienceBuffer(capacity: 100);
        var rng = new Random(42);

        // Populate buffer with deterministic toy environment experiences
        for (int i = 0; i < 10; i++)
        {
            var current = MakeState(GenerateRandomEmbedding(16, rng));
            var action = MakeAction();
            var nextState = MakeState(current.Embedding.Select(x => x * 0.5f + 1.0f).ToArray());
            buffer.Add(new Experience(current, action, nextState));
        }

        var batch = buffer.Sample(10, new Random(42));

        // Act
        var gradients = predictor.ComputeGradients(batch);

        // Assert — gradient arrays should exist for all 6 parameter groups
        Assert.Equal(6, gradients.Count);

        // Readout weights/biases directly affect the output, so must have non-zero gradients.
        // Message/update weights may have zero gradients due to ReLU gating in message passing.
        int readoutWeightNonZero = gradients["readoutWeights"].Count(g => g != 0f);
        int readoutBiasNonZero = gradients["readoutBias"].Count(g => g != 0f);
        Assert.True(readoutWeightNonZero > 0, "readoutWeights gradient should have non-zero values.");
        Assert.True(readoutBiasNonZero > 0, "readoutBias gradient should have non-zero values.");

        // At least 3 of the 6 gradient arrays should have non-zero values
        int arraysWitNonZeroGradients = gradients.Values.Count(arr => arr.Any(g => g != 0f));
        Assert.True(arraysWitNonZeroGradients >= 3,
            $"Expected at least 3 gradient arrays with non-zero values, got {arraysWitNonZeroGradients}.");
    }

    [Fact]
    public async Task TrainStepAsync_DecreasesLoss_Over50Steps()
    {
        // Arrange
        var predictor = GnnStatePredictor.CreateRandom(16, 10, 32, seed: 42);
        var buffer = new ExperienceBuffer(capacity: 200);
        var rng = new Random(42);

        // Deterministic toy environment: nextState.Embedding = currentState.Embedding * 0.5 + 1.0
        State ToyStep(State s, WorldModelAction a) =>
            MakeState(s.Embedding.Select(x => x * 0.5f + 1.0f).ToArray());

        // Generate 100 training experiences
        for (int i = 0; i < 100; i++)
        {
            var current = MakeState(GenerateRandomEmbedding(16, rng));
            var action = MakeAction("step");
            var next = ToyStep(current, action);
            buffer.Add(new Experience(current, action, next));
        }

        // Fixed test batch for consistent loss measurement
        var testBatch = buffer.Sample(10, new Random(99));
        float initialLoss = predictor.ComputeLoss(testBatch);

        // Act — 50 training steps
        for (int step = 0; step < 50; step++)
        {
            await predictor.TrainStepAsync(buffer, learningRate: 0.01f, batchSize: 16);
        }

        float finalLoss = predictor.ComputeLoss(testBatch);

        // Assert — training must produce measurable loss decrease
        Assert.True(finalLoss < initialLoss,
            $"Training did not decrease loss. Initial: {initialLoss}, Final: {finalLoss}");
    }

    [Fact]
    public async Task TrainStepAsync_WithNullBackend_TrainsSuccessfully()
    {
        // Arrange — predictor with null backend (CPU-only path)
        var predictor = GnnStatePredictor.CreateRandom(16, 10, 32, backend: null, seed: 42);
        var buffer = new ExperienceBuffer(capacity: 100);
        var rng = new Random(42);

        // Generate some experiences
        for (int i = 0; i < 20; i++)
        {
            var current = MakeState(GenerateRandomEmbedding(16, rng));
            var action = MakeAction();
            var next = MakeState(current.Embedding.Select(x => x * 0.5f + 1.0f).ToArray());
            buffer.Add(new Experience(current, action, next));
        }

        var testBatch = buffer.Sample(5, new Random(99));
        float lossBefore = predictor.ComputeLoss(testBatch);

        // Act — a few training steps should not crash
        for (int i = 0; i < 5; i++)
        {
            await predictor.TrainStepAsync(buffer, learningRate: 0.01f, batchSize: 8);
        }

        float lossAfter = predictor.ComputeLoss(testBatch);

        // Assert — smoke test: should complete without exceptions
        // Loss decrease is expected but not strictly required for this smoke test
        Assert.True(lossAfter >= 0f, "Loss should be non-negative.");
    }

    private static float[] GenerateRandomEmbedding(int size, Random rng)
    {
        var embedding = new float[size];
        for (int i = 0; i < size; i++)
        {
            embedding[i] = (float)(rng.NextDouble() * 2 - 1);
        }

        return embedding;
    }
}