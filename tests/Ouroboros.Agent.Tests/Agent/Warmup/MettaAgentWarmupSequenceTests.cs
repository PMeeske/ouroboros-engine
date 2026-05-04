// <copyright file="MettaAgentWarmupSequenceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Agent.Warmup;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Agent.Warmup;

[Trait("Category", "Unit")]
public class MettaAgentWarmupSequenceTests
{
    private static readonly float[] DummyEmbedding = new float[256];

    private readonly Mock<IMeTTaEngine> _engineMock = new();
    private readonly Mock<IPersonalityContextProvider> _personaMock = new();
    private readonly Mock<ILogger<MettaAgentWarmupSequence>> _loggerMock = new();
    private readonly RouteCentroidStore _routeStore = new(static _ => DummyEmbedding);

    private MettaAgentWarmupSequence CreateSut() => new(
        _engineMock.Object,
        _routeStore,
        _personaMock.Object,
        _loggerMock.Object);

    private void SetupAllQueriesSucceed(string payload = "(atom-a) (atom-b)")
    {
        _engineMock
            .Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success(payload));
    }

    [Fact]
    public async Task LoadSelfAtoms_AtomsPresent_ReturnsOk()
    {
        // Arrange
        SetupAllQueriesSucceed("(self) (purpose) (mood)");
        _personaMock.Setup(p => p.GetSelfAwarenessContext()).Returns("Iaret persona context");
        var sut = CreateSut();

        // Act
        var report = await sut.RunAsync(CancellationToken.None);

        // Assert
        var step = report.Steps.Single(s => s.Name == "load-self-atoms");
        step.Ok.Should().BeTrue();
        report.AtomsLoaded.Should().BeGreaterThan(0);
        _personaMock.Verify(p => p.GetSelfAwarenessContext(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProbeGroundedOps_BothQueriesSucceed_ReturnsOk()
    {
        // Arrange
        SetupAllQueriesSucceed();
        var sut = CreateSut();

        // Act
        var report = await sut.RunAsync(CancellationToken.None);

        // Assert
        var step = report.Steps.Single(s => s.Name == "probe-grounded-ops");
        step.Ok.Should().BeTrue();
        report.GroundedOpsOk.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task QuerySelfModel_LayersAcknowledged_ReturnsOk()
    {
        // Arrange
        SetupAllQueriesSucceed();
        var sut = CreateSut();

        // Act
        var report = await sut.RunAsync(CancellationToken.None);

        // Assert
        var step = report.Steps.Single(s => s.Name == "query-self-model");
        step.Ok.Should().BeTrue();
        // ConsciousnessLayer has 4 values: Legacy, ChatRepl, Autonomous, Companion.
        report.LayersAcknowledged.Should().Be(Enum.GetValues<ConsciousnessLayer>().Length);
    }

    [Fact]
    public async Task EmitRoutePrime_AllSeedRoutesPresent_ReturnsOk()
    {
        // Arrange
        SetupAllQueriesSucceed();
        var sut = CreateSut();

        // Act
        var report = await sut.RunAsync(CancellationToken.None);

        // Assert — RouteCentroidStore.SeedIfEmpty seeds ethics/causal/personality (Phase 186-04).
        var step = report.Steps.Single(s => s.Name == "emit-route-prime");
        step.Ok.Should().BeTrue();
        step.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReadinessReport_AnyStepFailed_DegradedTrue()
    {
        // Arrange — every query fails so probe-grounded-ops + load-self-atoms fail.
        _engineMock
            .Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Failure("engine offline"));
        var sut = CreateSut();

        // Act
        var report = await sut.RunAsync(CancellationToken.None);

        // Assert
        report.Degraded.Should().BeTrue();
        report.Steps.Should().HaveCount(5);
        report.Steps.Single(s => s.Name == "readiness-report").Ok.Should().BeTrue();
        report.Steps.Single(s => s.Name == "probe-grounded-ops").Ok.Should().BeFalse();
    }
}
