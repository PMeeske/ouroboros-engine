// <copyright file="TemporalSelfModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the TemporalSelfModel class.
/// </summary>
[Trait("Category", "Unit")]
public class TemporalSelfModelTests
{
    private readonly TemporalSelfModel _sut;

    public TemporalSelfModelTests()
    {
        _sut = new TemporalSelfModel();
    }

    // --- CaptureCurrentSelfAsync ---

    [Fact]
    public async Task CaptureCurrentSelfAsync_WithValidInputs_ReturnsSnapshot()
    {
        // Arrange
        var capabilities = new Dictionary<string, double> { ["reasoning"] = 0.8 };
        var beliefs = new Dictionary<string, double> { ["hypothesis-1"] = 0.9 };
        var traits = new Dictionary<string, double> { ["curiosity"] = 0.7 };

        // Act
        var result = await _sut.CaptureCurrentSelfAsync(capabilities, beliefs, traits);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Capabilities.Should().ContainKey("reasoning");
        result.Value.PersonalityTraits.Should().ContainKey("curiosity");
    }

    [Fact]
    public async Task CaptureCurrentSelfAsync_NullCapabilities_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.CaptureCurrentSelfAsync(
            null!,
            new Dictionary<string, double>(),
            new Dictionary<string, double>());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CaptureCurrentSelfAsync_NullBeliefs_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.CaptureCurrentSelfAsync(
            new Dictionary<string, double>(),
            null!,
            new Dictionary<string, double>());

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CaptureCurrentSelfAsync_NullTraits_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.CaptureCurrentSelfAsync(
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CaptureCurrentSelfAsync_TooSoon_ReturnsFailure()
    {
        // Arrange
        var capabilities = new Dictionary<string, double>();
        var beliefs = new Dictionary<string, double>();
        var traits = new Dictionary<string, double>();
        await _sut.CaptureCurrentSelfAsync(capabilities, beliefs, traits);

        // Act - immediately try again
        var result = await _sut.CaptureCurrentSelfAsync(capabilities, beliefs, traits);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("less than one minute");
    }

    // --- GetSelfTrajectoryAsync ---

    [Fact]
    public async Task GetSelfTrajectoryAsync_LessThanTwoSnapshots_ReturnsFailure()
    {
        // Arrange
        await CaptureSnapshot(new Dictionary<string, double> { ["skill"] = 0.5 });

        // Act
        var result = await _sut.GetSelfTrajectoryAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("At least two snapshots");
    }

    // --- ProjectFutureSelfAsync ---

    [Fact]
    public async Task ProjectFutureSelfAsync_LessThanTwoSnapshots_ReturnsFailure()
    {
        // Arrange
        await CaptureSnapshot(new Dictionary<string, double> { ["skill"] = 0.5 });

        // Act
        var result = await _sut.ProjectFutureSelfAsync(TimeSpan.FromDays(30));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("At least two snapshots");
    }

    // --- MeasureTemporalCoherence ---

    [Fact]
    public void MeasureTemporalCoherence_LessThanTwoSnapshots_ReturnsOne()
    {
        // Act
        var result = _sut.MeasureTemporalCoherence();

        // Assert
        result.Should().Be(1.0);
    }

    /// <summary>
    /// Helper method to capture a snapshot, bypassing the 1-minute cooldown
    /// by using reflection to manipulate timestamps. Since we can't easily
    /// bypass the cooldown in unit tests, this method just captures once.
    /// </summary>
    private async Task CaptureSnapshot(Dictionary<string, double> capabilities)
    {
        await _sut.CaptureCurrentSelfAsync(
            capabilities,
            new Dictionary<string, double>(),
            new Dictionary<string, double>());
    }
}
