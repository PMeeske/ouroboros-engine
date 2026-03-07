// <copyright file="MetaLearningEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Tests.MetaLearning;

[Trait("Category", "Unit")]
public class MetaLearningEngineTests
{
    private readonly Mock<IEmbeddingModel> _embeddingMock = new();

    [Fact]
    public void Constructor_NullEmbeddingModel_Throws()
    {
        var act = () => new MetaLearningEngine(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => new MetaLearningEngine(_embeddingMock.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithSeed_DoesNotThrow()
    {
        var act = () => new MetaLearningEngine(_embeddingMock.Object, seed: 42);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task MetaTrainAsync_NullTaskFamilies_ReturnsFailure()
    {
        var engine = CreateEngine();
        var config = MetaLearningConfig.DefaultMAML;

        var result = await engine.MetaTrainAsync(null!, config);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task MetaTrainAsync_EmptyTaskFamilies_ReturnsFailure()
    {
        var engine = CreateEngine();
        var config = MetaLearningConfig.DefaultMAML;

        var result = await engine.MetaTrainAsync(new List<TaskFamily>(), config);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AdaptToTaskAsync_NullExamples_ReturnsFailure()
    {
        var engine = CreateEngine();
        var metaModel = CreateDummyMetaModel();

        var result = await engine.AdaptToTaskAsync(metaModel, null!, 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task AdaptToTaskAsync_EmptyExamples_ReturnsFailure()
    {
        var engine = CreateEngine();
        var metaModel = CreateDummyMetaModel();

        var result = await engine.AdaptToTaskAsync(metaModel, new List<Example>(), 5);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AdaptToTaskAsync_ZeroSteps_ReturnsFailure()
    {
        var engine = CreateEngine();
        var metaModel = CreateDummyMetaModel();
        var examples = new List<Example> { new("input", "output") };

        var result = await engine.AdaptToTaskAsync(metaModel, examples, 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("positive");
    }

    [Fact]
    public async Task AdaptToTaskAsync_NegativeSteps_ReturnsFailure()
    {
        var engine = CreateEngine();
        var metaModel = CreateDummyMetaModel();
        var examples = new List<Example> { new("input", "output") };

        var result = await engine.AdaptToTaskAsync(metaModel, examples, -1);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MetaTrainAsync_CancelledToken_ReturnsFailure()
    {
        var engine = CreateEngine();
        var config = new MetaLearningConfig(MetaAlgorithm.MAML, 0.01, 0.001, 5, 4, 100);
        var families = CreateTaskFamilies();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await engine.MetaTrainAsync(families, config, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    private MetaLearningEngine CreateEngine()
    {
        return new MetaLearningEngine(_embeddingMock.Object, seed: 42);
    }

    private static MetaModel CreateDummyMetaModel()
    {
        var model = new SimpleModel(
            (input, _) => $"Response: {input}",
            new Dictionary<string, object> { ["bias"] = 0.0 });
        var config = MetaLearningConfig.DefaultMAML;
        return MetaModel.Create(model, config, new Dictionary<string, object> { ["bias"] = 0.0 });
    }

    private static List<TaskFamily> CreateTaskFamilies()
    {
        var examples = new List<Example>
        {
            new("hello", "world"),
            new("foo", "bar"),
        };
        var task = new SynthesisTask(Guid.NewGuid(), "test-task", "test", examples, examples, "Test task");
        var family = new TaskFamily("test-family",
            new List<SynthesisTask> { task },
            new List<SynthesisTask> { task },
            TaskDistribution.Uniform(new List<SynthesisTask> { task }));
        return new List<TaskFamily> { family };
    }
}
