// <copyright file="EdgeTtsServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class EdgeTtsServiceTests
{
    [Fact]
    public void ProviderName_ReturnsExpectedValue()
    {
        // Arrange
        using var sut = new EdgeTtsService();

        // Assert
        sut.ProviderName.Should().Be("Microsoft Edge TTS (Free Neural)");
    }

    [Fact]
    public void AvailableVoices_ContainsExpectedVoices()
    {
        // Arrange
        using var sut = new EdgeTtsService();

        // Assert
        sut.AvailableVoices.Should().Contain(EdgeTtsService.Voices.JennyNeural);
        sut.AvailableVoices.Should().Contain(EdgeTtsService.Voices.AriaNeural);
        sut.AvailableVoices.Should().Contain(EdgeTtsService.Voices.GuyNeural);
    }

    [Fact]
    public void SupportedFormats_ContainsExpectedFormats()
    {
        // Arrange
        using var sut = new EdgeTtsService();

        // Assert
        sut.SupportedFormats.Should().Contain("mp3");
        sut.SupportedFormats.Should().Contain("wav");
        sut.SupportedFormats.Should().Contain("ogg");
    }

    [Fact]
    public void MaxInputLength_Returns10000()
    {
        // Arrange
        using var sut = new EdgeTtsService();

        // Assert
        sut.MaxInputLength.Should().Be(10000);
    }

    [Fact]
    public void Voices_DefaultIsJennyNeural()
    {
        // Assert
        EdgeTtsService.Voices.Default.Should().Be("en-US-JennyNeural");
    }

    [Fact]
    public void Constructor_WithCustomVoice_DoesNotThrow()
    {
        // Act
        Action act = () =>
        {
            using var _ = new EdgeTtsService(voice: EdgeTtsService.Voices.SoniaNeural);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomOutputFormat_DoesNotThrow()
    {
        // Act
        Action act = () =>
        {
            using var _ = new EdgeTtsService(outputFormat: "audio-48khz-192kbitrate-mono-mp3");
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SynthesizeAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        using var sut = new EdgeTtsService();

        // Act
        var result = await sut.SynthesizeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text cannot be empty");
    }

    [Fact]
    public async Task SynthesizeAsync_WithWhitespaceText_ReturnsFailure()
    {
        // Arrange
        using var sut = new EdgeTtsService();

        // Act
        var result = await sut.SynthesizeAsync("   ");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text cannot be empty");
    }

    [Fact]
    public async Task SynthesizeSegmentsAsync_WithEmptySegments_ReturnsFailure()
    {
        // Arrange
        using var sut = new EdgeTtsService();
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>();

        // Act
        var result = await sut.SynthesizeSegmentsAsync(segments);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No segments to synthesize");
    }

    [Theory]
    [InlineData("cheerful", 5)]
    [InlineData("excited", 15)]
    [InlineData("sad", -10)]
    [InlineData("angry", 10)]
    [InlineData("whispering", -15)]
    [InlineData("calm", -5)]
    [InlineData("lyrical", -8)]
    [InlineData("chat", 0)]
    [InlineData("newscast-formal", -3)]
    [InlineData(null, 0)]
    [InlineData("unknown", 0)]
    public void MapStyleToProsody_ReturnsExpectedRatePercent(string? style, int expectedRate)
    {
        // Act
        var (ratePercent, _, _) = EdgeTtsService.MapStyleToProsody(style);

        // Assert
        ratePercent.Should().Be(expectedRate);
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithBreakSegment_IncludesRawSsml()
    {
        // Arrange
        using var sut = new EdgeTtsService();
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("Hello", null, null, null),
            ("<break time='500ms'/>", "break", null, null),
            ("World", null, null, null),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments);

        // Assert
        ssml.Should().Contain("<break time='500ms'/>");
        ssml.Should().Contain("Hello");
        ssml.Should().Contain("World");
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithStyle_AppliesProsody()
    {
        // Arrange
        using var sut = new EdgeTtsService();
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("Excited text", "excited", null, null),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments);

        // Assert
        ssml.Should().Contain("prosody");
        ssml.Should().Contain("Excited text");
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithEmptyWhitespaceText_SkipsSegment()
    {
        // Arrange
        using var sut = new EdgeTtsService();
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("   ", null, null, null),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments);

        // Assert
        // Should still have SSML structure but no prosody content for empty text
        ssml.Should().Contain("<speak");
        ssml.Should().Contain("</speak>");
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithPitchAndRateOverrides_IncludesOverrides()
    {
        // Arrange
        using var sut = new EdgeTtsService();
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("Custom", null, 0.1f, 1.2f),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments);

        // Assert
        ssml.Should().Contain("prosody");
        ssml.Should().Contain("Custom");
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var sut = new EdgeTtsService();

        // Act
        Action act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}
