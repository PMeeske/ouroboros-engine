namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class MonitoringAlertTests
{
    [Fact]
    public void HighPriority_CreatesPriority8Alert()
    {
        var alert = MonitoringAlert.HighPriority(
            "ErrorRate", "Error rate exceeded", Array.Empty<CognitiveEvent>(), "Reduce load");

        alert.Priority.Should().Be(8);
        alert.AlertType.Should().Be("ErrorRate");
        alert.Message.Should().Be("Error rate exceeded");
    }

    [Fact]
    public void MediumPriority_CreatesPriority5Alert()
    {
        var alert = MonitoringAlert.MediumPriority(
            "Latency", "Latency increased", Array.Empty<CognitiveEvent>(), "Optimize");

        alert.Priority.Should().Be(5);
    }

    [Fact]
    public void LowPriority_CreatesPriority2Alert()
    {
        var alert = MonitoringAlert.LowPriority(
            "Info", "Minor issue", Array.Empty<CognitiveEvent>(), "Monitor");

        alert.Priority.Should().Be(2);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidAlert()
    {
        var alert = MonitoringAlert.HighPriority(
            "Type", "Message", Array.Empty<CognitiveEvent>(), "Action");

        alert.Validate().IsSuccess.Should().BeTrue();
    }
}
