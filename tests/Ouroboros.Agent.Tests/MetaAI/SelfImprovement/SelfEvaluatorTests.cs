// <copyright file="SelfEvaluatorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class SelfEvaluatorTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<ICapabilityRegistry> _capsMock = new();
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();
    private readonly Mock<IMetaAIPlannerOrchestrator> _orchestratorMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new SelfEvaluator(
            null!, _capsMock.Object, _skillsMock.Object,
            _memoryMock.Object, _orchestratorMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullCapabilities_Throws()
    {
        var act = () => new SelfEvaluator(
            _llmMock.Object, null!, _skillsMock.Object,
            _memoryMock.Object, _orchestratorMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordPrediction_StoresCalibrationData()
    {
        var evaluator = CreateEvaluator();

        // Should not throw
        evaluator.RecordPrediction(0.8, true);
        evaluator.RecordPrediction(0.7, false);
    }

    [Fact]
    public async Task GetConfidenceCalibrationAsync_InsufficientData_Returns05()
    {
        var evaluator = CreateEvaluator();

        var result = await evaluator.GetConfidenceCalibrationAsync();

        result.Should().Be(0.5);
    }

    [Fact]
    public async Task GetConfidenceCalibrationAsync_WithData_ReturnsCalibration()
    {
        var evaluator = CreateEvaluator();

        // Record 15 well-calibrated predictions
        for (int i = 0; i < 15; i++)
        {
            evaluator.RecordPrediction(0.8, i < 12); // 80% actually succeed
        }

        var calibration = await evaluator.GetConfidenceCalibrationAsync();

        calibration.Should().BeGreaterThanOrEqualTo(0.0);
        calibration.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task GetPerformanceTrendAsync_UnknownMetric_ReturnsEmptyList()
    {
        var evaluator = CreateEvaluator();

        var trend = await evaluator.GetPerformanceTrendAsync("unknown_metric", TimeSpan.FromDays(7));

        trend.Should().BeEmpty();
    }

    private SelfEvaluator CreateEvaluator(SelfEvaluatorConfig? config = null)
    {
        return new SelfEvaluator(
            _llmMock.Object, _capsMock.Object, _skillsMock.Object,
            _memoryMock.Object, _orchestratorMock.Object, config);
    }
}
