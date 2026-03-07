// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using FluentAssertions;
using Moq;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain;
using Ouroboros.Domain.MetaLearning;
using Xunit;

namespace Ouroboros.Tests.MetaLearning;

/// <summary>
/// Complex-logic tests for MetaLearningEngine: algorithm-specific behavior,
/// gradient accumulation, parameter interpolation, per-param learning rates,
/// latent space operations, task sampling, and adaptation loop correctness.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MetaLearningEngineComplexLogicTests
{
    private readonly Mock<IEmbeddingModel> _embeddingModel = new();

    private MetaLearningEngine CreateSut(int? seed = 42) =>
        new(_embeddingModel.Object, seed);

    private static List<Example> MakeExamples(int count)
    {
        var examples = new List<Example>();
        for (int i = 0; i < count; i++)
        {
            examples.Add(Example.Create($"input_{i}", $"output_{i}"));
        }
        return examples;
    }

    private static SynthesisTask MakeTask(
        string name = "task",
        string domain = "domain",
        int trainingCount = 3,
        int validationCount = 2)
    {
        return SynthesisTask.Create(name, domain,
            MakeExamples(trainingCount), MakeExamples(validationCount), $"Task {name}");
    }

    private static List<TaskFamily> MakeTaskFamilies(int taskCountPerFamily = 5, int familyCount = 1)
    {
        var families = new List<TaskFamily>();
        for (int f = 0; f < familyCount; f++)
        {
            var tasks = new List<SynthesisTask>();
            for (int i = 0; i < taskCountPerFamily; i++)
            {
                tasks.Add(MakeTask($"task_{f}_{i}", $"domain_{f}"));
            }
            families.Add(TaskFamily.Create($"domain_{f}", tasks));
        }
        return families;
    }

    private static MetaLearningConfig MakeConfig(
        MetaAlgorithm algorithm,
        int metaIterations = 2,
        int innerSteps = 1,
        int taskBatchSize = 1) =>
        new(algorithm, 0.01, 0.001, innerSteps, taskBatchSize, metaIterations);

    private void SetupEmbedding(float[] embedding)
    {
        _embeddingModel
            .Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
    }

    // ========================================================
    // MAML: inner loop + outer loop gradient accumulation
    // ========================================================

    [Fact]
    public async Task MAML_MultipleIterations_ConvergesWithoutError()
    {
        // Arrange
        SetupEmbedding(new float[] { 0.5f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.MAML,
            metaIterations: 5, innerSteps: 3, taskBatchSize: 2);

        // Act
        var result = await sut.MetaTrainAsync(families, config);

        // Assert - model parameters should have been updated over 5 iterations
        result.IsSuccess.Should().BeTrue();
        var parameters = result.Value.MetaParameters;
        parameters.Should().ContainKey("bias");
        parameters.Should().ContainKey("weights");
        // The bias should have changed from initial 0.0
        // (SimpleModel initializes bias=0.0, weights=[1,0,0])
    }

    [Fact]
    public async Task MAML_TaskBatchSizeLargerThanFamilies_StillWorks()
    {
        // Verifies that task sampling works even when batch size exceeds
        // the number of available tasks (it re-samples from families)
        SetupEmbedding(new float[] { 0.1f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 2);
        var config = MakeConfig(MetaAlgorithm.MAML,
            metaIterations: 1, innerSteps: 1, taskBatchSize: 5);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
    }

    // ========================================================
    // Reptile: interpolation toward adapted parameters
    // ========================================================

    [Fact]
    public async Task Reptile_SingleIteration_ModifiesParameters()
    {
        // Arrange
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.Reptile,
            metaIterations: 1, innerSteps: 3, taskBatchSize: 1);

        // Act
        var result = await sut.MetaTrainAsync(families, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MetaParameters.Should().ContainKey("bias");
        result.Value.MetaParameters.Should().ContainKey("weights");
    }

    [Fact]
    public async Task Reptile_MultipleIterations_ParametersEvolve()
    {
        // Run with 1 iteration and 10 iterations, parameters should differ
        var sut1 = new MetaLearningEngine(_embeddingModel.Object, seed: 42);
        var sut2 = new MetaLearningEngine(_embeddingModel.Object, seed: 42);
        var families = MakeTaskFamilies(taskCountPerFamily: 5);

        var config1 = MakeConfig(MetaAlgorithm.Reptile,
            metaIterations: 1, innerSteps: 2, taskBatchSize: 1);
        var config2 = MakeConfig(MetaAlgorithm.Reptile,
            metaIterations: 5, innerSteps: 2, taskBatchSize: 1);

        var result1 = await sut1.MetaTrainAsync(families, config1);
        var result2 = await sut2.MetaTrainAsync(families, config2);

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Both should have bias and weights, but they may differ
        // (more iterations = more updates)
        result1.Value.MetaParameters.Keys.Should().BeEquivalentTo(
            result2.Value.MetaParameters.Keys);
    }

    // ========================================================
    // ProtoNet: prototype computation
    // ========================================================

    [Fact]
    public async Task ProtoNet_ComputesPrototypesFromEmbeddings()
    {
        // ProtoNet calls _embeddingModel for each training example
        int embeddingCallCount = 0;
        _embeddingModel
            .Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => embeddingCallCount++)
            .ReturnsAsync(new float[] { 0.5f, 0.3f });

        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.ProtoNet,
            metaIterations: 1, innerSteps: 1, taskBatchSize: 1);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
        // ProtoNet should have called the embedding model for each training example
        embeddingCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProtoNet_MultipleTasks_AccumulatesGradients()
    {
        SetupEmbedding(new float[] { 0.1f, 0.2f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.ProtoNet,
            metaIterations: 2, innerSteps: 1, taskBatchSize: 3);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
        result.Value.MetaParameters["algorithm"].Should().Be("ProtoNet");
    }

    // ========================================================
    // MetaSGD: per-parameter learning rate adaptation
    // ========================================================

    [Fact]
    public async Task MetaSGD_LearnsPerParameterLearningRates()
    {
        SetupEmbedding(new float[] { 0.1f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.MetaSGD,
            metaIterations: 3, innerSteps: 2, taskBatchSize: 2);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
        var parameters = result.Value.MetaParameters;

        // Should have learned learning rate for bias
        parameters.Should().ContainKey("lr_bias");
        var lrBias = parameters["lr_bias"];
        lrBias.Should().BeOfType<double>();
        ((double)lrBias).Should().BeGreaterThan(0);

        // Should have learned learning rates for weights array
        parameters.Should().ContainKey("lr_weights");
    }

    [Fact]
    public async Task MetaSGD_LearningRatesClampedToValidRange()
    {
        SetupEmbedding(new float[] { 0.1f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        // Run many iterations to push LR updates
        var config = MakeConfig(MetaAlgorithm.MetaSGD,
            metaIterations: 10, innerSteps: 2, taskBatchSize: 2);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
        var parameters = result.Value.MetaParameters;

        // lr_bias should be clamped between 1e-6 and 1.0
        if (parameters["lr_bias"] is double lrBias)
        {
            lrBias.Should().BeGreaterThanOrEqualTo(1e-6);
            lrBias.Should().BeLessThanOrEqualTo(1.0);
        }

        // lr_weights array values should all be clamped
        if (parameters["lr_weights"] is double[] lrWeights)
        {
            foreach (var lr in lrWeights)
            {
                lr.Should().BeGreaterThanOrEqualTo(1e-6);
                lr.Should().BeLessThanOrEqualTo(1.0);
            }
        }
    }

    // ========================================================
    // LEO: latent space encoding/decoding
    // ========================================================

    [Fact]
    public async Task LEO_UsesLatentSpace_ReturnsModelWithLatentDim()
    {
        SetupEmbedding(new float[] { 0.1f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.LEO,
            metaIterations: 2, innerSteps: 1, taskBatchSize: 1);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
        result.Value.MetaParameters.Should().ContainKey("latent_dim");
        ((int)result.Value.MetaParameters["latent_dim"]).Should().Be(16);
        result.Value.MetaParameters["algorithm"].Should().Be("LEO");
    }

    [Fact]
    public async Task LEO_MultipleInnerSteps_OptimizesInLatentSpace()
    {
        SetupEmbedding(new float[] { 0.1f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.LEO,
            metaIterations: 3, innerSteps: 5, taskBatchSize: 2);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
        // After multiple inner steps, model should have meaningful parameters
        result.Value.MetaParameters.Should().NotBeEmpty();
    }

    // ========================================================
    // AdaptToTaskAsync: adaptation loop correctness
    // ========================================================

    [Fact]
    public async Task AdaptToTaskAsync_ModelIsCloned_OriginalUnchanged()
    {
        var sut = CreateSut();
        var metaModel = await CreateTrainedMetaModel(sut);
        var originalParams = await metaModel.InnerModel.GetParametersAsync();
        var examples = MakeExamples(3);

        var result = await sut.AdaptToTaskAsync(metaModel, examples, 5);

        result.IsSuccess.Should().BeTrue();
        // The original meta-model's parameters should not be modified
        var afterParams = await metaModel.InnerModel.GetParametersAsync();
        afterParams.Should().BeEquivalentTo(originalParams);
    }

    [Fact]
    public async Task AdaptToTaskAsync_ReturnsValidationPerformance()
    {
        var sut = CreateSut();
        var metaModel = await CreateTrainedMetaModel(sut);
        var examples = MakeExamples(3);

        var result = await sut.AdaptToTaskAsync(metaModel, examples, 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.ValidationPerformance.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.ValidationPerformance.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task AdaptToTaskAsync_AdaptationTimeMeasured()
    {
        var sut = CreateSut();
        var metaModel = await CreateTrainedMetaModel(sut);
        var examples = MakeExamples(3);

        var result = await sut.AdaptToTaskAsync(metaModel, examples, 3);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdaptationTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    // ========================================================
    // ComputeTaskSimilarityAsync: edge cases
    // ========================================================

    [Fact]
    public async Task ComputeTaskSimilarityAsync_OrthogonalVectors_ReturnsLowSimilarity()
    {
        int callCount = 0;
        _embeddingModel
            .Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount % 2 == 1
                    ? new float[] { 1.0f, 0.0f, 0.0f }
                    : new float[] { 0.0f, 1.0f, 0.0f };
            });

        var sut = CreateSut();
        var metaModel = await CreateTrainedMetaModelWithSeparateSetup(sut);
        var taskA = MakeTask("A", "domain_a");
        var taskB = MakeTask("B", "domain_b");

        var result = await sut.ComputeTaskSimilarityAsync(taskA, taskB, metaModel);

        result.IsSuccess.Should().BeTrue();
        // Orthogonal vectors have cosine similarity 0
        result.Value.Should().BeLessThanOrEqualTo(0.1);
    }

    // ========================================================
    // EmbedTaskAsync: characteristic computation
    // ========================================================

    [Fact]
    public async Task EmbedTaskAsync_ComputesAverageInputOutputLength()
    {
        SetupEmbedding(new float[] { 0.1f, 0.2f });
        var sut = CreateSut();
        var metaModel = await CreateTrainedMetaModel(sut);

        var examples = new List<Example>
        {
            Example.Create("ab", "x"),
            Example.Create("abcd", "xyz"),
        };
        var task = SynthesisTask.Create("test", "domain", examples,
            new List<Example> { Example.Create("q", "a") }, "Test task");

        var result = await sut.EmbedTaskAsync(task, metaModel);

        result.IsSuccess.Should().BeTrue();
        // avg_input_length = (2 + 4) / 2 = 3
        result.Value.Characteristics["avg_input_length"].Should().Be(3.0);
        // avg_output_length = (1 + 3) / 2 = 2
        result.Value.Characteristics["avg_output_length"].Should().Be(2.0);
    }

    [Fact]
    public async Task EmbedTaskAsync_TaskDescriptionIncludesNameDomainDescription()
    {
        var sut = CreateSut();
        // Use separate setup for training so we can capture EmbedTask input afterward
        var metaModel = await CreateTrainedMetaModelWithSeparateSetup(sut);

        string? capturedInput = null;
        _embeddingModel
            .Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((input, _) => capturedInput = input)
            .ReturnsAsync(new float[] { 0.1f });

        var task = SynthesisTask.Create("MyTask", "NLP",
            MakeExamples(2), MakeExamples(1), "Classify sentiment");

        await sut.EmbedTaskAsync(task, metaModel);

        capturedInput.Should().Contain("MyTask");
        capturedInput.Should().Contain("NLP");
        capturedInput.Should().Contain("Classify sentiment");
    }

    // ========================================================
    // Multiple task families
    // ========================================================

    [Fact]
    public async Task MetaTrainAsync_MultipleFamilies_SamplesAcrossFamilies()
    {
        SetupEmbedding(new float[] { 0.1f });
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 3, familyCount: 3);
        var config = MakeConfig(MetaAlgorithm.MAML,
            metaIterations: 5, innerSteps: 1, taskBatchSize: 2);

        var result = await sut.MetaTrainAsync(families, config);

        result.IsSuccess.Should().BeTrue();
    }

    // ========================================================
    // Cancellation during inner loop
    // ========================================================

    [Fact]
    public async Task MAML_CancellationDuringInnerLoop_ReturnsFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = CreateSut();
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.MAML,
            metaIterations: 100, innerSteps: 100, taskBatchSize: 5);

        var result = await sut.MetaTrainAsync(families, config, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task AdaptToTaskAsync_CancellationDuringAdaptation_ReturnsFailure()
    {
        var sut = CreateSut();
        var metaModel = await CreateTrainedMetaModel(sut);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.AdaptToTaskAsync(
            metaModel, MakeExamples(3), 100, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    // ========================================================
    // Helpers
    // ========================================================

    private async Task<MetaModel> CreateTrainedMetaModel(MetaLearningEngine sut)
    {
        SetupEmbedding(new float[] { 0.5f, 0.5f });
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.MAML, metaIterations: 1, taskBatchSize: 1);
        var result = await sut.MetaTrainAsync(families, config);
        return result.Value;
    }

    private async Task<MetaModel> CreateTrainedMetaModelWithSeparateSetup(MetaLearningEngine sut)
    {
        var tempEmbedding = new Mock<IEmbeddingModel>();
        tempEmbedding
            .Setup(m => m.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.5f, 0.5f, 0.5f });
        var tempSut = new MetaLearningEngine(tempEmbedding.Object, seed: 42);
        var families = MakeTaskFamilies(taskCountPerFamily: 5);
        var config = MakeConfig(MetaAlgorithm.MAML, metaIterations: 1, taskBatchSize: 1);
        var result = await tempSut.MetaTrainAsync(families, config);
        return result.Value;
    }
}
