// <copyright file="MeTTaMemoryBridgeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.MeTTa;

[Trait("Category", "Unit")]
public class MeTTaMemoryBridgeTests
{
    private readonly Mock<IMeTTaEngine> _engineMock = new();
    private readonly MemoryStore _memoryStore = new();

    [Fact]
    public void Constructor_NullEngine_Throws()
    {
        var act = () => new MeTTaMemoryBridge(null!, _memoryStore);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMemory_Throws()
    {
        var act = () => new MeTTaMemoryBridge(_engineMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => new MeTTaMemoryBridge(_engineMock.Object, _memoryStore);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AddExperienceAsync_NullExperience_ReturnsFailure()
    {
        var bridge = CreateBridge();
        var result = await bridge.AddExperienceAsync(null!);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task QueryExperiencesAsync_DelegatesToEngine()
    {
        var bridge = CreateBridge();
        var expected = Result<string, string>.Success("results");
        _engineMock
            .Setup(e => e.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await bridge.QueryExperiencesAsync("(match &self $x $x)");

        result.IsSuccess.Should().BeTrue();
        _engineMock.Verify(
            e => e.ExecuteQueryAsync("(match &self $x $x)", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddVerificationRuleAsync_DelegatesToEngine()
    {
        var bridge = CreateBridge();
        var expected = Result<string, string>.Success("ok");
        _engineMock
            .Setup(e => e.ApplyRuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await bridge.AddVerificationRuleAsync("(= (verify $x) True)");

        result.IsSuccess.Should().BeTrue();
        _engineMock.Verify(
            e => e.ApplyRuleAsync("(= (verify $x) True)", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncAllExperiencesAsync_WhenEngineSucceeds_ReturnsFactCount()
    {
        var bridge = CreateBridge();
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var result = await bridge.SyncAllExperiencesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SyncAllExperiencesAsync_WhenEngineFailsAddFact_ReturnsFailure()
    {
        var bridge = CreateBridge();
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Failure("engine error")));

        var result = await bridge.SyncAllExperiencesAsync();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAllExperiencesAsync_WhenEngineThrows_ReturnsFailure()
    {
        var bridge = CreateBridge();
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await bridge.SyncAllExperiencesAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Memory sync failed");
    }

    private MeTTaMemoryBridge CreateBridge()
    {
        return new MeTTaMemoryBridge(_engineMock.Object, _memoryStore);
    }
}
