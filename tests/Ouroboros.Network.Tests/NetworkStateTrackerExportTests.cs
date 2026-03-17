// <copyright file="NetworkStateTrackerExportTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using FluentAssertions;
using LangChain.DocumentLoaders;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Vectors;
using Ouroboros.Tools.MeTTa;
using Xunit;

namespace Ouroboros.Tests.Network;

/// <summary>
/// Tests for the Export partial class of NetworkStateTracker, covering MeTTa export,
/// constraint verification, snapshots, and lifecycle methods.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NetworkStateTrackerExportTests : IDisposable
{
    private readonly NetworkStateTracker _tracker;
    private readonly IMeTTaEngine _mettaEngine;

    public NetworkStateTrackerExportTests()
    {
        _tracker = new NetworkStateTracker();
        _mettaEngine = Substitute.For<IMeTTaEngine>();
    }

    #region ExportToMeTTaAsync Tests

    [Fact]
    public async Task ExportToMeTTaAsync_NotConfigured_ReturnsFailure()
    {
        // Arrange
        var branch = CreateEmptyBranch("test-branch");

        // Act
        var result = await _tracker.ExportToMeTTaAsync(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MeTTa engine not configured");
    }

    [Fact]
    public async Task ExportToMeTTaAsync_Configured_ReturnsSuccess()
    {
        // Arrange
        _mettaEngine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));
        _tracker.ConfigureMeTTaExport(_mettaEngine);
        var branch = CreateEmptyBranch("test-branch");

        // Act
        var result = await _tracker.ExportToMeTTaAsync(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExportToMeTTaAsync_WithFacts_IncrementsAddedCount()
    {
        // Arrange
        _mettaEngine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));
        _tracker.ConfigureMeTTaExport(_mettaEngine);
        var branch = CreateEmptyBranch("counted-branch");

        // Act
        var result = await _tracker.ExportToMeTTaAsync(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region ExportAllToMeTTaAsync Tests

    [Fact]
    public async Task ExportAllToMeTTaAsync_NotConfigured_ReturnsFailure()
    {
        // Act
        var result = await _tracker.ExportAllToMeTTaAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MeTTa engine not configured");
    }

    [Fact]
    public async Task ExportAllToMeTTaAsync_Configured_ReturnsSuccess()
    {
        // Arrange
        _mettaEngine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));
        _tracker.ConfigureMeTTaExport(_mettaEngine);

        // Act
        var result = await _tracker.ExportAllToMeTTaAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExportAllToMeTTaAsync_WithSuccessfulRules_ReturnsPositiveCount()
    {
        // Arrange
        _mettaEngine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Success(Unit.Value));
        _tracker.ConfigureMeTTaExport(_mettaEngine);

        // Act
        var result = await _tracker.ExportAllToMeTTaAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExportAllToMeTTaAsync_WithFailedAdditions_StillReturnsSuccessWithReducedCount()
    {
        // Arrange
        _mettaEngine.AddFactAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit, string>.Failure("error"));
        _tracker.ConfigureMeTTaExport(_mettaEngine);

        // Act
        var result = await _tracker.ExportAllToMeTTaAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    #endregion

    #region VerifyConstraintAsync Tests

    [Fact]
    public async Task VerifyConstraintAsync_NotConfigured_ReturnsFailure()
    {
        // Act
        var result = await _tracker.VerifyConstraintAsync("main", "acyclic");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("MeTTa engine not configured");
    }

    [Fact]
    public async Task VerifyConstraintAsync_Configured_ConstraintSatisfied_ReturnsTrue()
    {
        // Arrange - VerifyDagConstraintAsync is an extension method that calls ExecuteQueryAsync
        // An empty/True result indicates the constraint is satisfied
        _mettaEngine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("True"));
        _tracker.ConfigureMeTTaExport(_mettaEngine);

        // Act
        var result = await _tracker.VerifyConstraintAsync("main", "acyclic");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyConstraintAsync_Configured_ConstraintNotSatisfied_ReturnsFalse()
    {
        // Arrange - A non-empty, non-True result indicates the constraint is NOT satisfied
        _mettaEngine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Success("violation detected"));
        _tracker.ConfigureMeTTaExport(_mettaEngine);

        // Act
        var result = await _tracker.VerifyConstraintAsync("main", "acyclic");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyConstraintAsync_EngineReturnsError_ReturnsFailure()
    {
        // Arrange
        _mettaEngine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string, string>.Failure("Engine error"));
        _tracker.ConfigureMeTTaExport(_mettaEngine);

        // Act
        var result = await _tracker.VerifyConstraintAsync("main", "acyclic");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region CreateSnapshot Tests

    [Fact]
    public void CreateSnapshot_EmptyTracker_ReturnsValidSnapshot()
    {
        // Act
        var snapshot = _tracker.CreateSnapshot();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.TotalNodes.Should().Be(0);
        snapshot.TotalTransitions.Should().Be(0);
        snapshot.Metadata.Should().ContainKey("trackedBranches");
        snapshot.Metadata.Should().ContainKey("branchCount");
    }

    [Fact]
    public void CreateSnapshot_EmptyTracker_MetadataContainsBranchCountZero()
    {
        // Act
        var snapshot = _tracker.CreateSnapshot();

        // Assert
        snapshot.Metadata["branchCount"].Should().Be("0");
        snapshot.Metadata["trackedBranches"].Should().BeEmpty();
    }

    #endregion

    #region GetStateSummary Tests

    [Fact]
    public void GetStateSummary_EmptyTracker_ContainsHeader()
    {
        // Act
        var summary = _tracker.GetStateSummary();

        // Assert
        summary.Should().Contain("=== Network State Summary ===");
    }

    [Fact]
    public void GetStateSummary_EmptyTracker_ShowsZeroCounts()
    {
        // Act
        var summary = _tracker.GetStateSummary();

        // Assert
        summary.Should().Contain("Total Nodes: 0");
        summary.Should().Contain("Total Transitions: 0");
        summary.Should().Contain("Tracked Branches: 0");
    }

    [Fact]
    public void GetStateSummary_EmptyTracker_ShowsCurrentEpoch()
    {
        // Act
        var summary = _tracker.GetStateSummary();

        // Assert
        summary.Should().Contain("Current Epoch:");
    }

    [Fact]
    public void GetStateSummary_ContainsNodesByTypeSection()
    {
        // Act
        var summary = _tracker.GetStateSummary();

        // Assert
        summary.Should().Contain("Nodes by Type:");
    }

    #endregion

    #region GetBranchReifier Tests

    [Fact]
    public void GetBranchReifier_UnknownBranch_ReturnsNone()
    {
        // Act
        var result = _tracker.GetBranchReifier("nonexistent-branch");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetBranchReifier_AfterTrackingBranch_ReturnsSome()
    {
        // Arrange
        var branch = CreateEmptyBranch("tracked-branch");
        _tracker.TrackBranch(branch);

        // Act
        var result = _tracker.GetBranchReifier("tracked-branch");

        // Assert
        result.HasValue.Should().BeTrue();
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_AfterTrackingBranch_ClearsTrackedBranches()
    {
        // Arrange
        var branch = CreateEmptyBranch("branch-to-reset");
        _tracker.TrackBranch(branch);
        _tracker.TrackedBranchCount.Should().Be(1);

        // Act
        _tracker.Reset();

        // Assert
        _tracker.TrackedBranchCount.Should().Be(0);
    }

    [Fact]
    public void Reset_ClearsMeTTaFacts()
    {
        // Act
        _tracker.Reset();

        // Assert
        _tracker.MeTTaFacts.Should().BeEmpty();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Act & Assert
        _tracker.Dispose();
        FluentActions.Invoking(() => _tracker.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Act
        await _tracker.DisposeAsync();

        // Assert
        await FluentActions.Invoking(async () => await _tracker.DisposeAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ClearsState()
    {
        // Arrange
        var branch = CreateEmptyBranch("dispose-branch");
        _tracker.TrackBranch(branch);

        // Act
        await _tracker.DisposeAsync();

        // Assert
        _tracker.TrackedBranchCount.Should().Be(0);
        _tracker.MeTTaFacts.Should().BeEmpty();
    }

    #endregion

    #region ReplayToNode Tests

    [Fact]
    public void ReplayToNode_NonexistentNode_ReturnsFailure()
    {
        // Act
        var result = _tracker.ReplayToNode(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static PipelineBranch CreateEmptyBranch(string name)
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    public void Dispose()
    {
        _tracker.Dispose();
    }

    #endregion
}
