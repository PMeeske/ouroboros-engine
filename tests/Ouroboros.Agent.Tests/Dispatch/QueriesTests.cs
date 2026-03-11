using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class QueriesTests
{
    [Fact]
    public void ClassifyUseCaseQuery_SetsPrompt()
    {
        // Arrange & Act
        var sut = new ClassifyUseCaseQuery("Analyze this data");

        // Assert
        sut.Prompt.Should().Be("Analyze this data");
    }

    [Fact]
    public void GetOrchestratorMetricsQuery_SetsOrchestratorName()
    {
        // Arrange & Act
        var sut = new GetOrchestratorMetricsQuery("SmartModelOrchestrator");

        // Assert
        sut.OrchestratorName.Should().Be("SmartModelOrchestrator");
    }

    [Fact]
    public void ValidateReadinessQuery_SetsOrchestratorName()
    {
        // Arrange & Act
        var sut = new ValidateReadinessQuery("PlannerOrchestrator");

        // Assert
        sut.OrchestratorName.Should().Be("PlannerOrchestrator");
    }
}
