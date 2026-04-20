// <copyright file="CognitiveBiasMonitorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the CognitiveBiasMonitor class.
/// </summary>
[Trait("Category", "Unit")]
public class CognitiveBiasMonitorTests
{
    private readonly CognitiveBiasMonitor _sut;

    public CognitiveBiasMonitorTests()
    {
        _sut = new CognitiveBiasMonitor();
    }

    // --- ScanForBiasesAsync ---

    [Fact]
    public async Task ScanForBiasesAsync_EmptyReasoning_ReturnsFailure()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ScanForBiasesAsync_NullReasoning_ReturnsFailure()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ScanForBiasesAsync_NoBiasIndicators_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "The data shows a clear pattern of increasing revenue.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanForBiasesAsync_ConfirmationBias_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "This confirms what I already believed about the system.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.ConfirmationBias);
    }

    [Fact]
    public async Task ScanForBiasesAsync_AnchoringBias_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "Based on the original value, the first estimate was close.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.AnchoringBias);
    }

    [Fact]
    public async Task ScanForBiasesAsync_AvailabilityHeuristic_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "I can easily recall a case where this happened before.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.AvailabilityHeuristic);
    }

    [Fact]
    public async Task ScanForBiasesAsync_DunningKruger_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "I'm certain this is obviously the right approach. No doubt about it.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.DunningKruger);
    }

    [Fact]
    public async Task ScanForBiasesAsync_DunningKruger_WithHedging_LowersConfidence()
    {
        // Act
        var withoutHedging = await _sut.ScanForBiasesAsync(
            "I'm certain this is obviously the right approach.");
        var monitor2 = new CognitiveBiasMonitor();
        var withHedging = await monitor2.ScanForBiasesAsync(
            "I'm certain this is obviously right, but perhaps I might be wrong.");

        // Assert
        var dkWithout = withoutHedging.Value.First(d => d.Type == BiasType.DunningKruger);
        var dkWith = withHedging.Value.First(d => d.Type == BiasType.DunningKruger);
        dkWith.Confidence.Should().BeLessThan(dkWithout.Confidence);
    }

    [Fact]
    public async Task ScanForBiasesAsync_SunkCostFallacy_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "We've already invested so much, it's too late to stop now.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.SunkCostFallacy);
    }

    [Fact]
    public async Task ScanForBiasesAsync_RecencyBias_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "Based on the most recent data, the latest data shows improvement just now.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.RecencyBias);
    }

    [Fact]
    public async Task ScanForBiasesAsync_BandwagonEffect_DetectsCorrectly()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "Everyone is doing it and popular opinion supports this approach.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(d => d.Type == BiasType.BandwagonEffect);
    }

    [Fact]
    public async Task ScanForBiasesAsync_MultipleIndicators_HigherConfidence()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "This confirms what I already believed, as expected, I knew it all along.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var detection = result.Value.First(d => d.Type == BiasType.ConfirmationBias);
        detection.Confidence.Should().BeGreaterThan(0.35);
    }

    [Fact]
    public async Task ScanForBiasesAsync_WithContext_BoostsConfidence()
    {
        // Act
        var withoutContext = await _sut.ScanForBiasesAsync(
            "This confirms what I already believed.");
        var monitor2 = new CognitiveBiasMonitor();
        var withContext = await monitor2.ScanForBiasesAsync(
            "This confirms what I already believed.",
            context: "As expected, the results match.");

        // Assert
        var confWithout = withoutContext.Value.First(d => d.Type == BiasType.ConfirmationBias).Confidence;
        var confWith = withContext.Value.First(d => d.Type == BiasType.ConfirmationBias).Confidence;
        confWith.Should().BeGreaterThanOrEqualTo(confWithout);
    }

    [Fact]
    public async Task ScanForBiasesAsync_ResultsOrderedByConfidence()
    {
        // Act
        var result = await _sut.ScanForBiasesAsync(
            "I'm certain this confirms what I already believed. Everyone is doing it. " +
            "I can easily recall examples. The most recent data shows this.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var confidences = result.Value.Select(d => d.Confidence).ToList();
        confidences.Should().BeInDescendingOrder();
    }

    // --- DebiasAsync ---

    [Fact]
    public async Task DebiasAsync_ConfirmationBias_SuggestsDisconfirmingEvidence()
    {
        // Arrange
        var scanResult = await _sut.ScanForBiasesAsync(
            "This confirms what I already believed.");
        var detection = scanResult.Value.First(d => d.Type == BiasType.ConfirmationBias);

        // Act
        var result = await _sut.DebiasAsync("original reasoning", detection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Debiased Reasoning");
        result.Value.Should().Contain("contradicts");
    }

    [Fact]
    public async Task DebiasAsync_SunkCostFallacy_SuggestsProspectiveEvaluation()
    {
        // Arrange
        var scanResult = await _sut.ScanForBiasesAsync(
            "We've already invested too much to stop now.");
        var detection = scanResult.Value.First(d => d.Type == BiasType.SunkCostFallacy);

        // Act
        var result = await _sut.DebiasAsync("reasoning", detection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("prospective");
    }

    // --- RecordBiasOutcome & GetAccuracyRates ---

    [Fact]
    public void RecordBiasOutcome_UnknownBiasId_ReturnsFailure()
    {
        // Act
        var result = _sut.RecordBiasOutcome("nonexistent-id", true);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RecordBiasOutcome_KnownBiasId_ReturnsSuccess()
    {
        // Arrange
        var scanResult = await _sut.ScanForBiasesAsync(
            "This confirms what I already believed.");
        var detection = scanResult.Value.First();

        // Act
        var result = _sut.RecordBiasOutcome(detection.Id, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GetAccuracyRates_NoOutcomes_ReturnsZeros()
    {
        // Act
        var (tpr, fpr, total) = _sut.GetAccuracyRates();

        // Assert
        tpr.Should().Be(0.0);
        fpr.Should().Be(0.0);
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetAccuracyRates_WithOutcomes_ReturnsCorrectRates()
    {
        // Arrange
        var scanResult = await _sut.ScanForBiasesAsync(
            "I'm certain this is obviously the right approach. This confirms what I already believed.");
        foreach (var detection in scanResult.Value)
        {
            _sut.RecordBiasOutcome(detection.Id, true);
        }

        // Act
        var (tpr, fpr, total) = _sut.GetAccuracyRates();

        // Assert
        total.Should().BeGreaterThan(0);
        tpr.Should().Be(1.0);
        fpr.Should().Be(0.0);
    }

    // --- GetDetections ---

    [Fact]
    public void GetDetections_NoScans_ReturnsEmpty()
    {
        // Act
        var result = _sut.GetDetections();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetections_FilterByType_ReturnsOnlyMatchingType()
    {
        // Arrange
        await _sut.ScanForBiasesAsync(
            "I'm certain this confirms what I already believed. Everyone is doing it.");

        // Act
        var result = _sut.GetDetections(BiasType.ConfirmationBias);

        // Assert
        result.Should().OnlyContain(d => d.Type == BiasType.ConfirmationBias);
    }

    [Fact]
    public async Task GetDetections_NoFilter_ReturnsAllDetections()
    {
        // Arrange
        await _sut.ScanForBiasesAsync(
            "I'm certain this confirms what I already believed. Everyone is doing it.");

        // Act
        var result = _sut.GetDetections();

        // Assert
        result.Should().HaveCountGreaterThan(1);
    }
}
