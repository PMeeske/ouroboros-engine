// <copyright file="MicrophoneRecorderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using FluentAssertions;
using Ouroboros.Providers.SpeechToText;
using Xunit;

[Trait("Category", "Unit")]
public class MicrophoneRecorderTests
{
    [Fact]
    public void IsRecordingAvailable_ReturnsBool()
    {
        // Act
        bool available = MicrophoneRecorder.IsRecordingAvailable();

        // Assert - just verify it doesn't throw
        available.Should().Be(available); // self-evident, but ensures no exception
    }

    [Fact]
    public async Task GetDeviceInfoAsync_ReturnsString()
    {
        // Act
        string info = await MicrophoneRecorder.GetDeviceInfoAsync();

        // Assert - it returns something (error message or device info)
        info.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordAsync_WithZeroDuration_CompletesOrFails()
    {
        // Arrange - trying to record with no recorder should fail gracefully
        // Act
        var result = await MicrophoneRecorder.RecordAsync(0);

        // Assert - either succeeds (if recorder found) or fails gracefully
        result.Should().NotBeNull();
    }
}
