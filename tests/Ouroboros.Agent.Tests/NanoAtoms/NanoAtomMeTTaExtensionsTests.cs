// <copyright file="NanoAtomMeTTaExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.NanoAtoms;

namespace Ouroboros.Agent.Tests.NanoAtoms;

/// <summary>
/// Unit tests for <see cref="NanoAtomMeTTaExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public class NanoAtomMeTTaExtensionsTests
{
    private readonly Mock<IMeTTaEngine> _engineMock = new();
    private readonly Mock<IChatCompletionModel> _modelMock = new();

    private NanoOuroborosAtom CreateAtom() => new(_modelMock.Object, NanoAtomConfig.Default());

    // --- AddNanoAtomStateAsync ---

    [Fact]
    public async Task AddNanoAtomStateAsync_NullEngine_ThrowsArgumentNullException()
    {
        using var atom = CreateAtom();
        IMeTTaEngine engine = null!;

        var act = () => engine.AddNanoAtomStateAsync(atom);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddNanoAtomStateAsync_NullAtom_ThrowsArgumentNullException()
    {
        var act = () => _engineMock.Object.AddNanoAtomStateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddNanoAtomStateAsync_WhenEngineSucceeds_ReturnsSuccess()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        using var atom = CreateAtom();

        // Act
        var result = await _engineMock.Object.AddNanoAtomStateAsync(atom);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s =>
                s.Contains("NanoAtomInstance") && s.Contains("InPhase")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddNanoAtomStateAsync_WhenEngineFails_ReturnsFailure()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Failure("engine error")));

        using var atom = CreateAtom();

        // Act
        var result = await _engineMock.Object.AddNanoAtomStateAsync(atom);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AddNanoAtomStateAsync_WhenEngineThrows_ReturnsFailure()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        using var atom = CreateAtom();

        // Act
        var result = await _engineMock.Object.AddNanoAtomStateAsync(atom);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("NanoAtom MeTTa translation error");
    }

    [Fact]
    public async Task AddNanoAtomStateAsync_IncludesCircuitClosedState()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        using var atom = CreateAtom();

        // Act
        await _engineMock.Object.AddNanoAtomStateAsync(atom);

        // Assert — new atom should have closed circuit
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s => s.Contains("CircuitClosed")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- AddThoughtFragmentAsync ---

    [Fact]
    public async Task AddThoughtFragmentAsync_NullEngine_ThrowsArgumentNullException()
    {
        var fragment = ThoughtFragment.FromText("Hello world");
        IMeTTaEngine engine = null!;

        var act = () => engine.AddThoughtFragmentAsync(fragment);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddThoughtFragmentAsync_NullFragment_ThrowsArgumentNullException()
    {
        var act = () => _engineMock.Object.AddThoughtFragmentAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddThoughtFragmentAsync_WithValidFragment_ReturnsSuccess()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var fragment = ThoughtFragment.FromText("Test fragment content");

        // Act
        var result = await _engineMock.Object.AddThoughtFragmentAsync(fragment);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s =>
                s.Contains("Fragment") && s.Contains("PreferredTier")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddThoughtFragmentAsync_WithStreamId_IncludesFlowsThrough()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var fragment = ThoughtFragment.FromText("Test content");

        // Act
        await _engineMock.Object.AddThoughtFragmentAsync(fragment, "stream-1");

        // Assert
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s =>
                s.Contains("FlowsThrough") && s.Contains("stream-1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddThoughtFragmentAsync_WithoutStreamId_DoesNotIncludeFlowsThrough()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var fragment = ThoughtFragment.FromText("Test content");

        // Act
        await _engineMock.Object.AddThoughtFragmentAsync(fragment);

        // Assert
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s => !s.Contains("FlowsThrough")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddThoughtFragmentAsync_WhenEngineThrows_ReturnsFailure()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var fragment = ThoughtFragment.FromText("Test content");

        // Act
        var result = await _engineMock.Object.AddThoughtFragmentAsync(fragment);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ThoughtFragment MeTTa translation error");
    }

    // --- AddDigestFragmentAsync ---

    [Fact]
    public async Task AddDigestFragmentAsync_NullEngine_ThrowsArgumentNullException()
    {
        var digest = CreateDigest("test");
        IMeTTaEngine engine = null!;

        var act = () => engine.AddDigestFragmentAsync(digest);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddDigestFragmentAsync_NullDigest_ThrowsArgumentNullException()
    {
        var act = () => _engineMock.Object.AddDigestFragmentAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddDigestFragmentAsync_WithValidDigest_ReturnsSuccess()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var digest = CreateDigest("Digested content");

        // Act
        var result = await _engineMock.Object.AddDigestFragmentAsync(digest);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s =>
                s.Contains("Digest") && s.Contains("Digests")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDigestFragmentAsync_WhenEngineThrows_ReturnsFailure()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("engine down"));

        var digest = CreateDigest("test");

        // Act
        var result = await _engineMock.Object.AddDigestFragmentAsync(digest);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("DigestFragment MeTTa translation error");
    }

    // --- AddConsolidatedActionAsync ---

    [Fact]
    public async Task AddConsolidatedActionAsync_NullEngine_ThrowsArgumentNullException()
    {
        var action = CreateAction("test");
        IMeTTaEngine engine = null!;

        var act = () => engine.AddConsolidatedActionAsync(action);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddConsolidatedActionAsync_NullAction_ThrowsArgumentNullException()
    {
        var act = () => _engineMock.Object.AddConsolidatedActionAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddConsolidatedActionAsync_WithValidAction_ReturnsSuccess()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var action = CreateAction("Consolidated result");

        // Act
        var result = await _engineMock.Object.AddConsolidatedActionAsync(action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _engineMock.Verify(
            e => e.AddFactAsync(It.Is<string>(s =>
                s.Contains("Action") && s.Contains("response")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddConsolidatedActionAsync_WhenEngineThrows_ReturnsFailure()
    {
        // Arrange
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection timeout"));

        var action = CreateAction("test");

        // Act
        var result = await _engineMock.Object.AddConsolidatedActionAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ConsolidatedAction MeTTa translation error");
    }

    // --- Helpers ---

    private static DigestFragment CreateDigest(string content) =>
        new(Guid.NewGuid(), Guid.NewGuid(), content, 2.0, 0.8, NanoAtomPhase.Emit, DateTime.UtcNow);

    private static ConsolidatedAction CreateAction(string content) =>
        new(Guid.NewGuid(), content, new List<DigestFragment>(), 0.85, "response", 2, 100, DateTime.UtcNow);
}
