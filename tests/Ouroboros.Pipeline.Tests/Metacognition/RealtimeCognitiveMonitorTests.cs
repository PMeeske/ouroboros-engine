using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class RealtimeCognitiveMonitorTests : IDisposable
{
    private readonly RealtimeCognitiveMonitor _monitor;

    public RealtimeCognitiveMonitorTests()
    {
        _monitor = new RealtimeCognitiveMonitor();
    }

    public void Dispose()
    {
        _monitor.Dispose();
    }

    [Fact]
    public void RecordEvent_WithValidEvent_ReturnsSuccess()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("A new idea");

        // Act
        var result = _monitor.RecordEvent(evt);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RecordEvent_WithNullEvent_ReturnsFailure()
    {
        // Act
        var result = _monitor.RecordEvent(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RecordEvent_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.Dispose();

        // Act
        var act = () => monitor.RecordEvent(CognitiveEvent.Thought("test"));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetRecentEvents_ReturnsRecordedEvents()
    {
        // Arrange
        _monitor.RecordEvent(CognitiveEvent.Thought("idea1"));
        _monitor.RecordEvent(CognitiveEvent.Decision("choice1"));
        _monitor.RecordEvent(CognitiveEvent.Insight("pattern1"));

        // Act
        var events = _monitor.GetRecentEvents(10);

        // Assert
        events.Should().HaveCount(3);
    }

    [Fact]
    public void GetRecentEvents_WithLimitSmallerThanBuffer_ReturnsLimitedResults()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
        {
            _monitor.RecordEvent(CognitiveEvent.Thought($"idea {i}"));
        }

        // Act
        var events = _monitor.GetRecentEvents(3);

        // Assert
        events.Should().HaveCount(3);
    }

    [Fact]
    public void GetRecentEvents_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.Dispose();

        // Act
        var act = () => monitor.GetRecentEvents(5);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetHealth_WithNoEvents_ReturnsHealthyStatus()
    {
        // Act
        var health = _monitor.GetHealth();

        // Assert
        health.Status.Should().Be(HealthStatus.Healthy);
        health.ErrorRate.Should().Be(0.0);
    }

    [Fact]
    public void GetHealth_WithProductiveEvents_ReturnsHighEfficiency()
    {
        // Arrange
        _monitor.RecordEvent(CognitiveEvent.Thought("idea"));
        _monitor.RecordEvent(CognitiveEvent.Decision("choice"));
        _monitor.RecordEvent(CognitiveEvent.Insight("pattern"));

        // Act
        var health = _monitor.GetHealth();

        // Assert
        health.ProcessingEfficiency.Should().Be(1.0);
        health.HealthScore.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void GetHealth_WithManyErrors_ReturnsDegradedOrWorseStatus()
    {
        // Arrange - all errors
        for (var i = 0; i < 10; i++)
        {
            _monitor.RecordEvent(CognitiveEvent.Error($"error {i}"));
        }

        // Act
        var health = _monitor.GetHealth();

        // Assert
        health.ErrorRate.Should().BeGreaterThan(0.0);
        health.Status.Should().NotBe(HealthStatus.Healthy);
    }

    [Fact]
    public void GetHealth_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.Dispose();

        // Act
        var act = () => monitor.GetHealth();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetAlerts_InitiallyEmpty()
    {
        // Act
        var alerts = _monitor.GetAlerts();

        // Assert
        alerts.Should().BeEmpty();
    }

    [Fact]
    public void GetAlerts_WithCriticalEvent_GeneratesAlert()
    {
        // Arrange
        var criticalEvent = CognitiveEvent.ContradictionDetected("Major conflict");

        // Act
        _monitor.RecordEvent(criticalEvent);
        var alerts = _monitor.GetAlerts();

        // Assert
        alerts.Should().NotBeEmpty();
        alerts.Should().Contain(a => a.AlertType == "CriticalEvent");
    }

    [Fact]
    public void AcknowledgeAlert_WithExistingAlert_ReturnsSuccess()
    {
        // Arrange
        _monitor.RecordEvent(CognitiveEvent.ContradictionDetected("conflict"));
        var alerts = _monitor.GetAlerts();
        alerts.Should().NotBeEmpty();
        var alertId = alerts[0].Id;

        // Act
        var result = _monitor.AcknowledgeAlert(alertId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _monitor.GetAlerts().Should().NotContain(a => a.Id == alertId);
    }

    [Fact]
    public void AcknowledgeAlert_WithNonExistingAlert_ReturnsFailure()
    {
        // Act
        var result = _monitor.AcknowledgeAlert(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AcknowledgeAlert_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.Dispose();

        // Act
        var act = () => monitor.AcknowledgeAlert(Guid.NewGuid());

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void SetThreshold_WithValidMetric_ReturnsSuccess()
    {
        // Act
        var result = _monitor.SetThreshold("custom_metric", 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var threshold = _monitor.GetThreshold("custom_metric");
        threshold.IsSome.Should().BeTrue();
        threshold.Value.Should().Be(0.5);
    }

    [Fact]
    public void SetThreshold_WithEmptyMetric_ReturnsFailure()
    {
        // Act
        var result = _monitor.SetThreshold("", 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SetThreshold_WithNegativeValue_ReturnsFailure()
    {
        // Act
        var result = _monitor.SetThreshold("metric", -1.0);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetThreshold_WithDefaultThresholds_ReturnsExpectedValues()
    {
        // Act & Assert
        _monitor.GetThreshold("error_rate").IsSome.Should().BeTrue();
        _monitor.GetThreshold("consecutive_errors").IsSome.Should().BeTrue();
        _monitor.GetThreshold("confusion_rate").IsSome.Should().BeTrue();
        _monitor.GetThreshold("contradiction_limit").IsSome.Should().BeTrue();
        _monitor.GetThreshold("latency_ms").IsSome.Should().BeTrue();
    }

    [Fact]
    public void GetThreshold_WithNonExistentMetric_ReturnsNone()
    {
        // Act
        var threshold = _monitor.GetThreshold("nonexistent");

        // Assert
        threshold.IsSome.Should().BeFalse();
    }

    [Fact]
    public void Subscribe_WithValidHandler_ReturnsDisposable()
    {
        // Act
        var subscription = _monitor.Subscribe(_ => { });

        // Assert
        subscription.Should().NotBeNull();
        subscription.Dispose(); // should not throw
    }

    [Fact]
    public void Subscribe_WithNullHandler_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _monitor.Subscribe(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Subscribe_NotifiesOnCriticalEvent()
    {
        // Arrange
        MonitoringAlert? receivedAlert = null;
        using var sub = _monitor.Subscribe(alert => receivedAlert = alert);

        // Act
        _monitor.RecordEvent(CognitiveEvent.ContradictionDetected("conflict"));

        // Assert
        receivedAlert.Should().NotBeNull();
        receivedAlert!.AlertType.Should().Be("CriticalEvent");
    }

    [Fact]
    public void Subscribe_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();
        monitor.Dispose();

        // Act
        var act = () => monitor.Subscribe(_ => { });

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Reset_ClearsEventsAndMetrics()
    {
        // Arrange
        _monitor.RecordEvent(CognitiveEvent.Thought("test"));
        _monitor.RecordEvent(CognitiveEvent.Error("error"));
        _monitor.RecordEvent(CognitiveEvent.ContradictionDetected("conflict"));

        // Act
        _monitor.Reset();

        // Assert
        _monitor.GetRecentEvents(100).Should().BeEmpty();
        _monitor.GetAlerts().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor();

        // Act
        var act = () =>
        {
            monitor.Dispose();
            monitor.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void BufferTrimming_TrimsWhenExceedingMaxSize()
    {
        // Arrange
        var monitor = new RealtimeCognitiveMonitor(maxBufferSize: 5);

        // Act
        for (var i = 0; i < 10; i++)
        {
            monitor.RecordEvent(CognitiveEvent.Thought($"idea {i}"));
        }

        // Assert
        var events = monitor.GetRecentEvents(100);
        events.Should().HaveCountLessOrEqualTo(5);

        monitor.Dispose();
    }

    [Fact]
    public void ConsecutiveErrors_GeneratesHighPriorityAlert()
    {
        // Arrange
        MonitoringAlert? receivedAlert = null;
        using var sub = _monitor.Subscribe(alert => receivedAlert = alert);

        // Act - record 3+ consecutive errors (default threshold is 3)
        for (var i = 0; i < 4; i++)
        {
            _monitor.RecordEvent(CognitiveEvent.Error($"error {i}"));
        }

        // Assert
        var alerts = _monitor.GetAlerts();
        alerts.Should().Contain(a => a.AlertType == "CriticalEvent" || a.AlertType == "ConsecutiveErrors");
    }

    [Fact]
    public void HighConfusionRate_GeneratesMediumPriorityAlert()
    {
        // Arrange - set a low threshold so fewer events trigger it
        _monitor.SetThreshold("confusion_rate", 0.3);

        // Act - create a high confusion rate
        for (var i = 0; i < 5; i++)
        {
            _monitor.RecordEvent(CognitiveEvent.Confusion($"confused {i}"));
        }

        // Assert
        var alerts = _monitor.GetAlerts();
        alerts.Should().Contain(a => a.AlertType == "HighConfusionRate");
    }

    [Fact]
    public void ContradictionOverload_GeneratesAlert()
    {
        // Arrange
        _monitor.SetThreshold("contradiction_limit", 2);

        // Act
        _monitor.RecordEvent(CognitiveEvent.ContradictionDetected("conflict 1"));
        _monitor.RecordEvent(CognitiveEvent.ContradictionDetected("conflict 2"));

        // Assert
        var alerts = _monitor.GetAlerts();
        // Should have CriticalEvent alerts and possibly ContradictionOverload
        alerts.Should().NotBeEmpty();
    }

    [Fact]
    public void LatencyTracking_RecordsLatencyFromContext()
    {
        // Arrange
        var context = ImmutableDictionary<string, object>.Empty.Add("latency_ms", 500.0);
        var evt = CognitiveEvent.Thought("test", context);

        // Act
        _monitor.RecordEvent(evt);
        var health = _monitor.GetHealth();

        // Assert
        health.ResponseLatency.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void DuplicateAlertSuppression_DoesNotDuplicateWithin30Seconds()
    {
        // Arrange - record two critical events rapidly
        var criticalEvt1 = CognitiveEvent.ContradictionDetected("conflict 1");

        // Act
        _monitor.RecordEvent(criticalEvt1);
        var alertsAfterFirst = _monitor.GetAlerts().Count;

        _monitor.RecordEvent(CognitiveEvent.ContradictionDetected("conflict 2"));
        var alertsAfterSecond = _monitor.GetAlerts();

        // Assert - CriticalEvent type should not be duplicated within 30 seconds
        // There might be ContradictionOverload alerts too, so just check CriticalEvent isn't duplicated
        var criticalEventAlerts = alertsAfterSecond.Count(a => a.AlertType == "CriticalEvent");
        criticalEventAlerts.Should().BeLessThanOrEqualTo(1);
    }
}
