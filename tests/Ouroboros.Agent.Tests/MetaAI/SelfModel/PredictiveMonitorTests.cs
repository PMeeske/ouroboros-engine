// <copyright file="PredictiveMonitorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the PredictiveMonitor self-calibration component.
/// </summary>
[Trait("Category", "Unit")]
public class PredictiveMonitorTests
{
    private readonly PredictiveMonitor _sut;

    public PredictiveMonitorTests()
    {
        _sut = new PredictiveMonitor();
    }

    [Fact]
    public void CreateForecast_ReturnsForecastWithPendingStatus()
    {
        // Arrange
        string description = "CPU usage forecast";
        string metricName = "cpu_usage";
        double predictedValue = 75.0;
        double confidence = 0.85;
        DateTime targetTime = DateTime.UtcNow.AddHours(1);

        // Act
        Forecast result = _sut.CreateForecast(description, metricName, predictedValue, confidence, targetTime);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be(description);
        result.MetricName.Should().Be(metricName);
        result.PredictedValue.Should().Be(predictedValue);
        result.Confidence.Should().Be(confidence);
        result.Status.Should().Be(ForecastStatus.Pending);
        result.ActualValue.Should().BeNull();
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateForecast_ClampsConfidenceToRange()
    {
        // Arrange & Act
        Forecast tooHigh = _sut.CreateForecast("test", "metric", 10.0, 1.5, DateTime.UtcNow.AddHours(1));
        Forecast tooLow = _sut.CreateForecast("test", "metric", 10.0, -0.5, DateTime.UtcNow.AddHours(1));

        // Assert
        tooHigh.Confidence.Should().Be(1.0);
        tooLow.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void CreateForecast_NullDescription_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => _sut.CreateForecast(null!, "metric", 10.0, 0.8, DateTime.UtcNow.AddHours(1)))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateForecast_NullMetricName_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => _sut.CreateForecast("desc", null!, 10.0, 0.8, DateTime.UtcNow.AddHours(1)))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateForecastOutcome_WithinTenPercent_MarksAsVerified()
    {
        // Arrange
        Forecast forecast = _sut.CreateForecast("test", "metric", 100.0, 0.9, DateTime.UtcNow.AddHours(1));

        // Act — actual value is 95.0, within 10% of predicted 100.0
        _sut.UpdateForecastOutcome(forecast.Id, 95.0);

        // Assert
        List<Forecast> all = _sut.GetForecastsByMetric("metric");
        Forecast updated = all.First(f => f.Id == forecast.Id);
        updated.Status.Should().Be(ForecastStatus.Verified);
        updated.ActualValue.Should().Be(95.0);
    }

    [Fact]
    public void UpdateForecastOutcome_BeyondTenPercent_MarksAsFailed()
    {
        // Arrange
        Forecast forecast = _sut.CreateForecast("test", "metric", 100.0, 0.9, DateTime.UtcNow.AddHours(1));

        // Act — actual value is 80.0, 20% off from predicted 100.0
        _sut.UpdateForecastOutcome(forecast.Id, 80.0);

        // Assert
        List<Forecast> all = _sut.GetForecastsByMetric("metric");
        Forecast updated = all.First(f => f.Id == forecast.Id);
        updated.Status.Should().Be(ForecastStatus.Failed);
        updated.ActualValue.Should().Be(80.0);
    }

    [Fact]
    public void GetPendingForecasts_ReturnsOnlyPendingOrderedByTargetTime()
    {
        // Arrange
        Forecast f1 = _sut.CreateForecast("first", "m", 10.0, 0.8, DateTime.UtcNow.AddHours(3));
        Forecast f2 = _sut.CreateForecast("second", "m", 20.0, 0.8, DateTime.UtcNow.AddHours(1));
        Forecast f3 = _sut.CreateForecast("third", "m", 30.0, 0.8, DateTime.UtcNow.AddHours(2));

        // Resolve one so it is no longer pending
        _sut.UpdateForecastOutcome(f1.Id, 10.0);

        // Act
        List<Forecast> pending = _sut.GetPendingForecasts();

        // Assert
        pending.Should().HaveCount(2);
        pending.Should().NotContain(f => f.Id == f1.Id);
        pending[0].TargetTime.Should().BeBefore(pending[1].TargetTime);
    }

    [Fact]
    public void GetForecastsByMetric_FiltersByMetricNameCaseInsensitive()
    {
        // Arrange
        _sut.CreateForecast("cpu", "CPU_Usage", 50.0, 0.7, DateTime.UtcNow.AddHours(1));
        _sut.CreateForecast("mem", "Memory", 70.0, 0.8, DateTime.UtcNow.AddHours(1));
        _sut.CreateForecast("cpu2", "cpu_usage", 55.0, 0.7, DateTime.UtcNow.AddHours(2));

        // Act
        List<Forecast> cpuForecasts = _sut.GetForecastsByMetric("cpu_usage");

        // Assert
        cpuForecasts.Should().HaveCount(2);
        cpuForecasts.Should().OnlyContain(f => f.MetricName.Equals("cpu_usage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetForecastsByMetric_ExcludesCompleted_WhenRequested()
    {
        // Arrange
        Forecast f1 = _sut.CreateForecast("test1", "metric_a", 10.0, 0.8, DateTime.UtcNow.AddHours(1));
        _sut.CreateForecast("test2", "metric_a", 20.0, 0.8, DateTime.UtcNow.AddHours(2));
        _sut.UpdateForecastOutcome(f1.Id, 10.0); // Mark as verified

        // Act
        List<Forecast> pendingOnly = _sut.GetForecastsByMetric("metric_a", includeCompleted: false);

        // Assert
        pendingOnly.Should().HaveCount(1);
        pendingOnly.Should().NotContain(f => f.Id == f1.Id);
    }

    [Fact]
    public void GetCalibration_NoCompletedForecasts_ReturnsZeroCalibration()
    {
        // Arrange — only create pending forecasts, never resolve them
        _sut.CreateForecast("test", "metric", 10.0, 0.8, DateTime.UtcNow.AddHours(1));

        // Act
        ForecastCalibration calibration = _sut.GetCalibration(TimeSpan.FromDays(1));

        // Assert
        calibration.TotalForecasts.Should().Be(0);
        calibration.VerifiedForecasts.Should().Be(0);
        calibration.FailedForecasts.Should().Be(0);
        calibration.BrierScore.Should().Be(0.0);
        calibration.CalibrationError.Should().Be(0.0);
    }

    [Fact]
    public void GetCalibration_WithMixedOutcomes_ComputesBrierScoreAndCalibrationError()
    {
        // Arrange — create and resolve multiple forecasts
        Forecast good = _sut.CreateForecast("good", "metric", 100.0, 0.9, DateTime.UtcNow.AddHours(1));
        Forecast bad = _sut.CreateForecast("bad", "metric", 100.0, 0.9, DateTime.UtcNow.AddHours(1));

        _sut.UpdateForecastOutcome(good.Id, 98.0);  // Within 10% -> Verified
        _sut.UpdateForecastOutcome(bad.Id, 50.0);    // Beyond 10% -> Failed

        // Act
        ForecastCalibration calibration = _sut.GetCalibration(TimeSpan.FromDays(1));

        // Assert
        calibration.TotalForecasts.Should().Be(2);
        calibration.VerifiedForecasts.Should().Be(1);
        calibration.FailedForecasts.Should().Be(1);
        calibration.BrierScore.Should().BeGreaterThan(0.0);
        calibration.AverageConfidence.Should().BeApproximately(0.9, 0.001);
        calibration.MetricAccuracies.Should().ContainKey("metric");
    }

    [Fact]
    public async Task DetectAnomalyAsync_InsufficientData_ReturnsNonAnomaly()
    {
        // Arrange — no historical data recorded for the metric

        // Act
        AnomalyDetection result = await _sut.DetectAnomalyAsync("new_metric", 50.0);

        // Assert
        result.IsAnomaly.Should().BeFalse();
        result.Severity.Should().Be("Normal");
        result.MetricName.Should().Be("new_metric");
        result.ObservedValue.Should().Be(50.0);
    }

    [Fact]
    public async Task DetectAnomalyAsync_ValueBeyondTwoStdDevs_DetectsAnomaly()
    {
        // Arrange — build up a tight history around 50.0 by recording forecast outcomes
        for (int i = 0; i < 10; i++)
        {
            Forecast f = _sut.CreateForecast($"hist{i}", "stable_metric", 50.0, 0.8, DateTime.UtcNow.AddHours(1));
            _sut.UpdateForecastOutcome(f.Id, 50.0 + (i % 2 == 0 ? 0.5 : -0.5));
        }

        // Act — observe a value far from the mean
        AnomalyDetection result = await _sut.DetectAnomalyAsync("stable_metric", 200.0);

        // Assert
        result.IsAnomaly.Should().BeTrue();
        result.Severity.Should().NotBe("Normal");
        result.Deviation.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task ForecastMetricAsync_NoHistoricalData_ReturnsFailure()
    {
        // Act
        Result<Forecast, string> result = await _sut.ForecastMetricAsync("unknown_metric", TimeSpan.FromHours(1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No historical data");
    }

    [Fact]
    public void ValidatePendingForecasts_CancelsOverdueForecasts()
    {
        // Arrange — create a forecast whose target time is already past
        Forecast overdue = _sut.CreateForecast(
            "overdue",
            "metric",
            10.0,
            0.8,
            DateTime.UtcNow.AddSeconds(-1));

        Forecast future = _sut.CreateForecast(
            "future",
            "metric",
            20.0,
            0.8,
            DateTime.UtcNow.AddHours(1));

        // Act
        int validated = _sut.ValidatePendingForecasts();

        // Assert
        validated.Should().BeGreaterThanOrEqualTo(1);
        List<Forecast> all = _sut.GetForecastsByMetric("metric");
        Forecast overdueResult = all.First(f => f.Id == overdue.Id);
        overdueResult.Status.Should().Be(ForecastStatus.Cancelled);
        Forecast futureResult = all.First(f => f.Id == future.Id);
        futureResult.Status.Should().Be(ForecastStatus.Pending);
    }
}
