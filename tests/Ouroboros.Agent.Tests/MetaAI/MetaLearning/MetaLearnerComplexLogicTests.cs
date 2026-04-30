// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Ouroboros.Agent.MetaAI.MetaLearning;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MetaAI.MetaLearning;

/// <summary>
/// Complex-logic tests for MetaLearner: strategy optimization with LLM parsing,
/// few-shot adaptation, hyperparameter suggestion from history vs defaults,
/// learning efficiency evaluation, learning speed trend, bottleneck detection,
/// meta-knowledge extraction, and episode recording.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MetaLearnerComplexLogicTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();

    private MetaLearner CreateSut(MetaLearnerConfig? config = null) =>
        new(_llmMock.Object, _skillsMock.Object, _memoryMock.Object, config);

    private static LearningStrategy MakeStrategy(
        string name = "TestStrategy",
        LearningApproach approach = LearningApproach.Supervised) =>
        new(name, "A test strategy", approach,
            new HyperparameterConfig(0.01, 16, 100, 0.8, 0.1, new Dictionary<string, object>()),
            new List<string> { "general" }, 0.8,
            new Dictionary<string, object>());

    private static LearningEpisode MakeEpisode(
        string taskType = "classification",
        bool successful = true,
        double performance = 0.85,
        int iterations = 50,
        int examples = 10,
        string? failureReason = null,
        DateTime? startedAt = null,
        DateTime? completedAt = null)
    {
        var start = startedAt ?? DateTime.UtcNow.AddHours(-1);
        var completed = completedAt ?? DateTime.UtcNow;
        return new LearningEpisode(
            Guid.NewGuid(),
            taskType,
            $"Learn {taskType} task",
            MakeStrategy(),
            examples,
            iterations,
            performance,
            completed - start,
            new List<PerformanceSnapshot>(),
            successful,
            failureReason,
            start,
            completed);
    }

    // ========================================================
    // OptimizeLearningStrategyAsync
    // ========================================================

    [Fact]
    public async Task OptimizeStrategy_WithMinEpisodes_CallsLLMAndParsesResponse()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "NAME: AdaptiveGradient\n" +
                "APPROACH: MetaGradient\n" +
                "TASKS: classification, reasoning\n" +
                "EFFICIENCY: 85%\n" +
                "OPTIMIZATIONS:\n- Use curriculum learning\n- Increase batch size");

        var sut = CreateSut(new MetaLearnerConfig(MinEpisodesForOptimization: 3));
        var history = Enumerable.Range(0, 5)
            .Select(_ => MakeEpisode())
            .ToList();

        var result = await sut.OptimizeLearningStrategyAsync(history);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("AdaptiveGradient");
        result.Value.Approach.Should().Be(LearningApproach.MetaGradient);
        result.Value.SuitableTaskTypes.Should().Contain("classification");
        result.Value.SuitableTaskTypes.Should().Contain("reasoning");
        result.Value.ExpectedEfficiency.Should().BeApproximately(0.85, 0.001);
    }

    [Fact]
    public async Task OptimizeStrategy_NoSuccessfulEpisodes_ReturnsFailure()
    {
        var sut = CreateSut(new MetaLearnerConfig(MinEpisodesForOptimization: 2));
        var history = Enumerable.Range(0, 5)
            .Select(_ => MakeEpisode(successful: false, failureReason: "timeout"))
            .ToList();

        var result = await sut.OptimizeLearningStrategyAsync(history);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No successful episodes");
    }

    [Fact]
    public async Task OptimizeStrategy_InsufficientEpisodesForCustomConfig_ReturnsFailure()
    {
        var sut = CreateSut(new MetaLearnerConfig(MinEpisodesForOptimization: 50));
        var history = Enumerable.Range(0, 10)
            .Select(_ => MakeEpisode())
            .ToList();

        var result = await sut.OptimizeLearningStrategyAsync(history);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient");
        result.Error.Should().Contain("50");
    }

    [Fact]
    public async Task OptimizeStrategy_LLMThrows_ReturnsFailure()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var sut = CreateSut(new MetaLearnerConfig(MinEpisodesForOptimization: 2));
        var history = Enumerable.Range(0, 5)
            .Select(_ => MakeEpisode())
            .ToList();

        var result = await sut.OptimizeLearningStrategyAsync(history);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    [Fact]
    public async Task OptimizeStrategy_UnrecognizedApproach_DefaultsToSupervised()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NAME: Test\nAPPROACH: QuantumLearning\nTASKS: general\nEFFICIENCY: 50%");

        var sut = CreateSut(new MetaLearnerConfig(MinEpisodesForOptimization: 2));
        var history = Enumerable.Range(0, 3)
            .Select(_ => MakeEpisode())
            .ToList();

        var result = await sut.OptimizeLearningStrategyAsync(history);

        result.IsSuccess.Should().BeTrue();
        result.Value.Approach.Should().Be(LearningApproach.Supervised);
    }

    [Fact]
    public async Task OptimizeStrategy_ComputesHyperparametersFromHistory()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NAME: Test\nAPPROACH: Supervised\nTASKS: general\nEFFICIENCY: 80%");

        var sut = CreateSut(new MetaLearnerConfig(MinEpisodesForOptimization: 2));
        var history = new List<LearningEpisode>
        {
            MakeEpisode(iterations: 40, examples: 20, performance: 0.9),
            MakeEpisode(iterations: 60, examples: 30, performance: 0.8),
        };

        var result = await sut.OptimizeLearningStrategyAsync(history);

        result.IsSuccess.Should().BeTrue();
        // BatchSize = avg examples = (20+30)/2 = 25
        result.Value.Hyperparameters.BatchSize.Should().Be(25);
        // MaxIterations = avg iterations = (40+60)/2 = 50
        result.Value.Hyperparameters.MaxIterations.Should().Be(50);
        // QualityThreshold = avg performance * 0.9 = 0.85 * 0.9 = 0.765
        result.Value.Hyperparameters.QualityThreshold.Should().BeApproximately(0.765, 0.01);
    }

    // ========================================================
    // FewShotAdaptAsync
    // ========================================================

    [Fact]
    public async Task FewShotAdapt_EmptyDescription_ReturnsFailure()
    {
        var sut = CreateSut();
        var examples = new List<TaskExample>
        {
            new("input", "output"),
        };

        var result = await sut.FewShotAdaptAsync("", examples);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task FewShotAdapt_NoExamples_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.FewShotAdaptAsync("classify images", new List<TaskExample>());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("At least one example");
    }

    [Fact]
    public async Task FewShotAdapt_LimitsExamplesToMaxFewShotExamples()
    {
        string? capturedPrompt = null;
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((prompt, _) => capturedPrompt = prompt)
            .ReturnsAsync("PATTERNS: pattern1\nSTEPS:\n1. Do something\nESTIMATED_PERFORMANCE: 0.9");

        var sut = CreateSut(new MetaLearnerConfig(MaxFewShotExamples: 3));
        var examples = Enumerable.Range(0, 10)
            .Select(i => new TaskExample($"input_{i}", $"output_{i}", Importance: i * 0.1))
            .ToList();

        var result = await sut.FewShotAdaptAsync("classify images", examples, maxExamples: 5);

        result.IsSuccess.Should().BeTrue();
        // Should be limited to min(maxExamples=5, MaxFewShotExamples=3) = 3
        result.Value.ExamplesUsed.Should().Be(3);
    }

    [Fact]
    public async Task FewShotAdapt_PrioritizesByImportance()
    {
        string? capturedPrompt = null;
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((prompt, _) => capturedPrompt = prompt)
            .ReturnsAsync("PATTERNS: p\nSTEPS:\n1. Step\nESTIMATED_PERFORMANCE: 0.8");

        var sut = CreateSut(new MetaLearnerConfig(MaxFewShotExamples: 10));
        var examples = new List<TaskExample>
        {
            new("low_importance", "out", Importance: 0.1),
            new("high_importance", "out", Importance: 0.9),
            new("medium_importance", "out", Importance: 0.5),
        };

        await sut.FewShotAdaptAsync("test task", examples, maxExamples: 2);

        // The prompt should include high_importance first (sorted desc by importance)
        capturedPrompt.Should().Contain("high_importance");
    }

    [Fact]
    public async Task FewShotAdapt_ParsesPatterns()
    {
        // Use a whole number for EstimatedPerformance to avoid locale-dependent
        // parsing issues (German locale treats "." as thousands separator).
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "PATTERNS: input-output mapping, regex extraction\n" +
                "STEPS:\n1. Parse input\n2. Apply regex\n" +
                "PREREQUISITES: regex library\n" +
                "ESTIMATED_PERFORMANCE: 1");

        var sut = CreateSut();
        var examples = new List<TaskExample> { new("input", "output") };

        var result = await sut.FewShotAdaptAsync("extract data", examples);

        result.IsSuccess.Should().BeTrue();
        result.Value.LearnedPatterns.Should().Contain("input-output mapping");
        result.Value.LearnedPatterns.Should().Contain("regex extraction");
        result.Value.EstimatedPerformance.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task FewShotAdapt_ParsesSteps()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "PATTERNS: pattern\nSTEPS:\n1. First step\n2. Second step\n" +
                "ESTIMATED_PERFORMANCE: 0.8");

        var sut = CreateSut();
        var examples = new List<TaskExample> { new("input", "output") };

        var result = await sut.FewShotAdaptAsync("task", examples);

        result.IsSuccess.Should().BeTrue();
        result.Value.AdaptedSkill.Steps.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task FewShotAdapt_LLMThrows_ReturnsFailure()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        var sut = CreateSut();
        var examples = new List<TaskExample> { new("input", "output") };

        var result = await sut.FewShotAdaptAsync("task", examples);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    // ========================================================
    // SuggestHyperparametersAsync
    // ========================================================

    [Fact]
    public async Task SuggestHyperparameters_EmptyTaskType_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.SuggestHyperparametersAsync("");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SuggestHyperparameters_NoHistory_ReturnsDefaults()
    {
        var sut = CreateSut();

        var result = await sut.SuggestHyperparametersAsync("classification");

        result.IsSuccess.Should().BeTrue();
        result.Value.LearningRate.Should().Be(0.01);
        result.Value.BatchSize.Should().Be(32);
        result.Value.MaxIterations.Should().Be(100);
        result.Value.QualityThreshold.Should().Be(0.85);
        result.Value.ExplorationRate.Should().Be(0.05);
    }

    [Fact]
    public async Task SuggestHyperparameters_GenerationDefaults_DifferFromClassification()
    {
        var sut = CreateSut();

        var classResult = await sut.SuggestHyperparametersAsync("classification");
        var genResult = await sut.SuggestHyperparametersAsync("generation");

        classResult.IsSuccess.Should().BeTrue();
        genResult.IsSuccess.Should().BeTrue();
        genResult.Value.LearningRate.Should().NotBe(classResult.Value.LearningRate);
        genResult.Value.BatchSize.Should().NotBe(classResult.Value.BatchSize);
    }

    [Fact]
    public async Task SuggestHyperparameters_ReasoningDefaults()
    {
        var sut = CreateSut();
        var result = await sut.SuggestHyperparametersAsync("reasoning");

        result.IsSuccess.Should().BeTrue();
        result.Value.LearningRate.Should().Be(0.005);
        result.Value.BatchSize.Should().Be(8);
    }

    [Fact]
    public async Task SuggestHyperparameters_UnknownTaskType_ReturnsFallbackDefaults()
    {
        var sut = CreateSut();
        var result = await sut.SuggestHyperparametersAsync("some_unknown_type");

        result.IsSuccess.Should().BeTrue();
        result.Value.LearningRate.Should().Be(0.01);
        result.Value.BatchSize.Should().Be(16);
    }

    [Fact]
    public async Task SuggestHyperparameters_WithHistory_UsesHistoricalData()
    {
        var sut = CreateSut();
        // Record episodes to build history
        for (int i = 0; i < 5; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                taskType: "classification",
                iterations: 80,
                examples: 24,
                performance: 0.92));
        }

        var result = await sut.SuggestHyperparametersAsync("classification");

        result.IsSuccess.Should().BeTrue();
        // Should use historical averages, not defaults
        result.Value.BatchSize.Should().Be(24); // avg examples = 24
        result.Value.MaxIterations.Should().Be(80); // avg iterations = 80
    }

    [Fact]
    public async Task SuggestHyperparameters_PassesContextAsCustomParams()
    {
        var sut = CreateSut();
        var context = new Dictionary<string, object>
        {
            ["gpu_memory"] = 8192,
            ["deadline"] = "2025-01-01",
        };

        var result = await sut.SuggestHyperparametersAsync("classification", context);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomParams.Should().ContainKey("gpu_memory");
        result.Value.CustomParams["gpu_memory"].Should().Be(8192);
    }

    // ========================================================
    // EvaluateLearningEfficiencyAsync
    // ========================================================

    [Fact]
    public async Task EvaluateEfficiency_NoRecentEpisodes_ReturnsFailure()
    {
        var sut = CreateSut();

        var result = await sut.EvaluateLearningEfficiencyAsync(TimeSpan.FromDays(7));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No learning episodes");
    }

    [Fact]
    public async Task EvaluateEfficiency_ComputesSuccessRate()
    {
        var sut = CreateSut();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 7; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                successful: true, startedAt: now.AddHours(-1), completedAt: now));
        }
        for (int i = 0; i < 3; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                successful: false, startedAt: now.AddHours(-1), completedAt: now));
        }

        var result = await sut.EvaluateLearningEfficiencyAsync(TimeSpan.FromDays(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.SuccessRate.Should().BeApproximately(0.7, 0.001);
    }

    [Fact]
    public async Task EvaluateEfficiency_DetectsHighIterationBottleneck()
    {
        var sut = CreateSut();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                iterations: 200, startedAt: now.AddHours(-1), completedAt: now));
        }

        var result = await sut.EvaluateLearningEfficiencyAsync(TimeSpan.FromDays(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Bottlenecks.Should().Contain(b => b.Contains("High iteration count"));
    }

    [Fact]
    public async Task EvaluateEfficiency_DetectsLowSuccessRateBottleneck()
    {
        var sut = CreateSut();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 8; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                successful: false, startedAt: now.AddHours(-1), completedAt: now));
        }
        for (int i = 0; i < 2; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                successful: true, startedAt: now.AddHours(-1), completedAt: now));
        }

        var result = await sut.EvaluateLearningEfficiencyAsync(TimeSpan.FromDays(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Bottlenecks.Should().Contain(b => b.Contains("Low success rate"));
    }

    [Fact]
    public async Task EvaluateEfficiency_ComputesLearningSpeedTrend()
    {
        var sut = CreateSut();
        var now = DateTime.UtcNow;
        // First half: high iterations (slow learning)
        for (int i = 0; i < 5; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                iterations: 100,
                startedAt: now.AddHours(-(10 - i)),
                completedAt: now.AddHours(-(9 - i))));
        }
        // Second half: low iterations (fast learning)
        for (int i = 0; i < 5; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                iterations: 20,
                startedAt: now.AddHours(-(5 - i)),
                completedAt: now.AddHours(-(4 - i))));
        }

        var result = await sut.EvaluateLearningEfficiencyAsync(TimeSpan.FromDays(1));

        result.IsSuccess.Should().BeTrue();
        // Trend should be positive (improvement: fewer iterations in second half)
        result.Value.LearningSpeedTrend.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateEfficiency_GeneratesRecommendationsForLowSuccessRate()
    {
        var sut = CreateSut();
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                successful: false, startedAt: now.AddHours(-1), completedAt: now));
        }
        sut.RecordLearningEpisode(MakeEpisode(
            successful: true, startedAt: now.AddHours(-1), completedAt: now));

        var result = await sut.EvaluateLearningEfficiencyAsync(TimeSpan.FromDays(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Recommendations.Should().Contain(r => r.Contains("quality"));
    }

    // ========================================================
    // ExtractMetaKnowledgeAsync
    // ========================================================

    [Fact]
    public async Task ExtractMetaKnowledge_NoEpisodes_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var result = await sut.ExtractMetaKnowledgeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMetaKnowledge_RequiresMinimum3SuccessfulPerTaskType()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("INSIGHT: Test insight\nCONFIDENCE: 0.8\nAPPLICABLE_TO: classification");

        var sut = CreateSut();
        // Only 2 successful classification episodes - not enough
        sut.RecordLearningEpisode(MakeEpisode(taskType: "classification", successful: true));
        sut.RecordLearningEpisode(MakeEpisode(taskType: "classification", successful: true));
        // 1 failed
        sut.RecordLearningEpisode(MakeEpisode(taskType: "classification", successful: false));

        var result = await sut.ExtractMetaKnowledgeAsync();

        result.IsSuccess.Should().BeTrue();
        // Should not have extracted insights (need 3+ successful)
        result.Value.Should().NotContain(k => k.Domain == "classification");
    }

    [Fact]
    public async Task ExtractMetaKnowledge_WithEnoughEpisodes_ParsesInsights()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "INSIGHT: Smaller batch sizes work better\nCONFIDENCE: 0.9\n" +
                "APPLICABLE_TO: classification, reasoning\n" +
                "INSIGHT: Early stopping prevents overfitting\nCONFIDENCE: 0.75\n" +
                "APPLICABLE_TO: generation");

        var sut = CreateSut();
        for (int i = 0; i < 5; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(taskType: "classification", successful: true));
        }

        var result = await sut.ExtractMetaKnowledgeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Value.Should().Contain(k => k.Insight.Contains("batch sizes"));
        result.Value.Should().Contain(k => k.Insight.Contains("Early stopping"));
    }

    [Fact]
    public async Task ExtractMetaKnowledge_ManySuccessfulEpisodes_AddsGeneralInsight()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("INSIGHT: Works well\nCONFIDENCE: 0.8\nAPPLICABLE_TO: all");

        var sut = CreateSut();
        // 20+ episodes with 70%+ success rate
        for (int i = 0; i < 16; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(
                taskType: i % 2 == 0 ? "classification" : "reasoning",
                successful: true));
        }
        for (int i = 0; i < 4; i++)
        {
            sut.RecordLearningEpisode(MakeEpisode(successful: false));
        }

        var result = await sut.ExtractMetaKnowledgeAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(
            k => k.Domain == "General" && k.Insight.Contains("effective"));
    }

    // ========================================================
    // RecordLearningEpisode
    // ========================================================

    [Fact]
    public void RecordLearningEpisode_NullEpisode_ThrowsArgumentNull()
    {
        var sut = CreateSut();
        var act = () => sut.RecordLearningEpisode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordLearningEpisode_ValidEpisode_CanBeQueriedLater()
    {
        var sut = CreateSut();
        sut.RecordLearningEpisode(MakeEpisode(taskType: "test_type"));

        // Verify by asking for hyperparameters for this task type
        var result = sut.SuggestHyperparametersAsync("test_type").Result;
        result.IsSuccess.Should().BeTrue();
        // Should use recorded history rather than defaults
        result.Value.BatchSize.Should().Be(10); // from MakeEpisode default
    }
}
