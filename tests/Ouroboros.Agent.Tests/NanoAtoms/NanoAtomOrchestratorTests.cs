// <copyright file="NanoAtomOrchestratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Tests for NanoAtomOrchestrator — IComposableOrchestrator integration.
/// </summary>
[Trait("Category", "Unit")]
public class NanoAtomOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_ValidPrompt_ReturnsSuccessResult()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Orchestrated response");

        var config = new NanoAtomConfig(UseGoalDecomposer: false);
        var orchestrator = new NanoAtomOrchestrator(mockModel.Object, config);

        // Act
        var result = await orchestrator.ExecuteAsync("Hello, what is AI?");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Output!.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyPrompt_ReturnsFailure()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var config = NanoAtomConfig.Default();
        var orchestrator = new NanoAtomOrchestrator(mockModel.Object, config);

        // Act
        var result = await orchestrator.ExecuteAsync("   ");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public void Map_TransformsOutput()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var config = NanoAtomConfig.Default();
        var orchestrator = new NanoAtomOrchestrator(mockModel.Object, config);

        // Act: Map should return a new composable orchestrator
        var mapped = orchestrator.Map(action => action.Content);

        // Assert
        mapped.Should().NotBeNull();
        mapped.Should().BeAssignableTo<IComposableOrchestrator<string, string>>();
    }

    [Fact]
    public void Tap_ReturnsTappedOrchestrator()
    {
        // Arrange
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var config = NanoAtomConfig.Default();
        var orchestrator = new NanoAtomOrchestrator(mockModel.Object, config);

        bool tapped = false;

        // Act
        var tappedOrchestrator = orchestrator.Tap(_ => tapped = true);

        // Assert
        tappedOrchestrator.Should().NotBeNull();
        tappedOrchestrator.Should().BeAssignableTo<IComposableOrchestrator<string, ConsolidatedAction>>();
    }
}
