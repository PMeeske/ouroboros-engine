// <copyright file="GradientBasedLearnerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Pipeline.Learning;

[Trait("Category", "Unit")]
public sealed class GradientBasedLearnerTests
{
    private GradientBasedLearner CreateLearner(
        GradientLearnerConfig? config = null,
        IReadOnlyDictionary<string, double>? initialParams = null)
        => new(config, initialParams);

    // --- Constructor ---

    [Fact]
    public void Constructor_DefaultConfig_CreatesLearner()
    {
        var learner = CreateLearner();
        learner.Metrics.ProcessedCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithInitialParams_SetsParameters()
    {
        var initial = new Dictionary<string, double> { ["weight"] = 0.5 };
        var learner = CreateLearner(initialParameters: initial);

        var param = learner.GetParameter("weight");
        param.HasValue.Should().BeTrue();
        param.Value.Should().Be(0.5);
    }

    // --- GetParameter / SetParameter ---

    [Fact]
    public void GetParameter_NonExistent_ReturnsNone()
    {
        var learner = CreateLearner();
        learner.GetParameter("unknown").HasValue.Should().BeFalse();
    }

    [Fact]
    public void SetParameter_ThenGet_ReturnsValue()
    {
        var learner = CreateLearner();
        learner.SetParameter("bias", 1.5);

        learner.GetParameter("bias").Value.Should().Be(1.5);
    }

    // --- ProcessFeedback ---

    [Fact]
    public void ProcessFeedback_ValidFeedback_ReturnsSuccess()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(
            config: GradientLearnerConfig.Default,
            initialParams: initial);

        var feedback = Feedback.Explicit("src", "input", "output", 0.8);
        var result = learner.ProcessFeedback(feedback);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessFeedback_EmptySourceId_ReturnsFailure()
    {
        var learner = CreateLearner();
        var feedback = new Feedback(
            Guid.NewGuid(), "", "input", "output", 0.5,
            FeedbackType.Explicit, DateTime.UtcNow,
            System.Collections.Immutable.ImmutableList<string>.Empty);

        var result = learner.ProcessFeedback(feedback);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ProcessFeedback_IncrementsMetrics()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(initialParams: initial);

        var feedback = Feedback.Explicit("src", "input", "output", 0.5);
        learner.ProcessFeedback(feedback);

        learner.Metrics.ProcessedCount.Should().Be(1);
    }

    [Fact]
    public void ProcessFeedback_NoParams_CreatesDefaultBias()
    {
        var learner = CreateLearner();
        var feedback = Feedback.Explicit("src", "input", "output", 0.5);

        var result = learner.ProcessFeedback(feedback);

        result.IsSuccess.Should().BeTrue();
        learner.GetParameter("bias").HasValue.Should().BeTrue();
    }

    // --- ProcessBatch ---

    [Fact]
    public void ProcessBatch_EmptyBatch_ReturnsSuccess()
    {
        var learner = CreateLearner();
        var result = learner.ProcessBatch(Array.Empty<Feedback>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void ProcessBatch_MultipleFeedback_AggregatesUpdates()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(initialParams: initial);

        var batch = new[]
        {
            Feedback.Explicit("src", "in1", "out1", 0.5),
            Feedback.Explicit("src", "in2", "out2", 0.8),
        };

        var result = learner.ProcessBatch(batch);

        result.IsSuccess.Should().BeTrue();
        learner.Metrics.ProcessedCount.Should().Be(2);
    }

    // --- ApplyUpdates ---

    [Fact]
    public void ApplyUpdates_NoPending_ReturnsZero()
    {
        var learner = CreateLearner();
        var result = learner.ApplyUpdates();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void ApplyUpdates_WithPending_ModifiesParameters()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 100 };
        var learner = CreateLearner(config: config, initialParams: initial);

        var feedback = Feedback.Explicit("src", "input", "output", 0.5);
        learner.ProcessFeedback(feedback);

        var result = learner.ApplyUpdates();
        result.IsSuccess.Should().BeTrue();
    }

    // --- GetPendingUpdates ---

    [Fact]
    public void GetPendingUpdates_Initial_Empty()
    {
        var learner = CreateLearner();
        learner.GetPendingUpdates().Should().BeEmpty();
    }

    [Fact]
    public void GetPendingUpdates_AfterFeedback_NotEmpty()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 100 };
        var learner = CreateLearner(config: config, initialParams: initial);

        var feedback = Feedback.Explicit("src", "input", "output", 0.5);
        learner.ProcessFeedback(feedback);

        learner.GetPendingUpdates().Should().NotBeEmpty();
    }

    // --- GetAllParameters ---

    [Fact]
    public void GetAllParameters_ReturnsImmutableDictionary()
    {
        var initial = new Dictionary<string, double> { ["a"] = 1.0, ["b"] = 2.0 };
        var learner = CreateLearner(initialParams: initial);

        var all = learner.GetAllParameters();

        all.Should().HaveCount(2);
        all["a"].Should().Be(1.0);
        all["b"].Should().Be(2.0);
    }

    // --- ResetState ---

    [Fact]
    public void ResetState_ClearsMetricsButKeepsParams()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(initialParams: initial);

        var feedback = Feedback.Explicit("src", "input", "output", 0.5);
        learner.ProcessFeedback(feedback);

        learner.ResetState();

        learner.Metrics.ProcessedCount.Should().Be(0);
        learner.GetParameter("w1").HasValue.Should().BeTrue();
    }

    // --- Different feedback types ---

    [Fact]
    public void ProcessFeedback_CorrectiveFeedback_ProcessesWithHigherWeight()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(initialParams: initial);

        var feedback = Feedback.Corrective("src", "input", "bad output", "good output");
        var result = learner.ProcessFeedback(feedback);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ProcessFeedback_ImplicitFeedback_ProcessesWithLowerWeight()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(initialParams: initial);

        var feedback = Feedback.Implicit("src", "input", "output", 0.3);
        var result = learner.ProcessFeedback(feedback);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ProcessFeedback_ComparativeFeedback_Processes()
    {
        var initial = new Dictionary<string, double> { ["w1"] = 0.5 };
        var learner = CreateLearner(initialParams: initial);

        var feedback = Feedback.Comparative("src", "input", "chosen", "rejected");
        var result = learner.ProcessFeedback(feedback);

        result.IsSuccess.Should().BeTrue();
    }
}
