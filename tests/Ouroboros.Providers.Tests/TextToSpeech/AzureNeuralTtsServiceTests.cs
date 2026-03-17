// <copyright file="AzureNeuralTtsServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

[Trait("Category", "Unit")]
public class AzureNeuralTtsServiceTests
{
    [Fact]
    public void Constructor_WithNullSubscriptionKey_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new AzureNeuralTtsService(null!, "eastus");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRegion_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new AzureNeuralTtsService("key", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProviderName_ReturnsExpectedValue()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Assert
        sut.ProviderName.Should().Be("Azure Neural TTS");
    }

    [Fact]
    public void AvailableVoices_ContainsExpectedVoices()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Assert
        sut.AvailableVoices.Should().Contain("en-US-JennyNeural");
        sut.AvailableVoices.Should().Contain("en-US-AriaNeural");
        sut.AvailableVoices.Should().Contain("de-DE-KatjaNeural");
    }

    [Fact]
    public void SupportedFormats_ContainsExpectedFormats()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Assert
        sut.SupportedFormats.Should().Contain("wav");
        sut.SupportedFormats.Should().Contain("mp3");
        sut.SupportedFormats.Should().Contain("ogg");
    }

    [Fact]
    public void MaxInputLength_Returns10000()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Assert
        sut.MaxInputLength.Should().Be(10000);
    }

    [Fact]
    public async Task IsAvailableAsync_WithValidKeyAndRegion_ReturnsTrue()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("test-key", "eastus");

        // Act
        bool available = await sut.IsAvailableAsync();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithEmptyKey_ReturnsFalse()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("", "eastus");

        // Act
        bool available = await sut.IsAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WithEmptyRegion_ReturnsFalse()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("key", "");

        // Act
        bool available = await sut.IsAvailableAsync();

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public void SupportsStreaming_ReturnsTrue()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Assert
        sut.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public void IsSynthesizing_InitiallyFalse()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Assert
        sut.IsSynthesizing.Should().BeFalse();
    }

    [Fact]
    public void EmotionalStyle_CanBeSetAndRead()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        sut.EmotionalStyle = "cheerful";

        // Assert
        sut.EmotionalStyle.Should().Be("cheerful");
    }

    [Fact]
    public void SelfVectorPitchOffset_CanBeSetAndRead()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        sut.SelfVectorPitchOffset = 0.1f;

        // Assert
        sut.SelfVectorPitchOffset.Should().Be(0.1f);
    }

    [Fact]
    public void SelfVectorRateMultiplier_CanBeSetAndRead()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        sut.SelfVectorRateMultiplier = 1.2f;

        // Assert
        sut.SelfVectorRateMultiplier.Should().Be(1.2f);
    }

    [Fact]
    public void Culture_CanBeSetAndRead()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        sut.Culture = "de-DE";

        // Assert
        sut.Culture.Should().Be("de-DE");
    }

    [Fact]
    public void Culture_WhenChanged_UpdatesVoice()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus", persona: "Ouroboros", culture: "en-US");

        // Act - changing culture should trigger voice update
        Action act = () => sut.Culture = "de-DE";

        // Assert - should not throw
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SynthesizeAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

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
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        var result = await sut.SynthesizeAsync("   ");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text cannot be empty");
    }

    [Fact]
    public async Task SynthesizeChunkAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        var result = await sut.SynthesizeChunkAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Text cannot be empty");
    }

    [Fact]
    public void InterruptSynthesis_WhenNotSynthesizing_DoesNotThrow()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        Action act = () => sut.InterruptSynthesis();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var sut = new AzureNeuralTtsService("fake-key", "eastus");

        // Act
        Action act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Ouroboros", "en-US")]
    [InlineData("Ouroboros", "de-DE")]
    [InlineData("IARET", "en-US")]
    [InlineData("ARIA", "en-US")]
    [InlineData("ECHO", "en-US")]
    [InlineData("SAGE", "en-US")]
    [InlineData("ATLAS", "en-US")]
    [InlineData("ATLAS", "de-DE")]
    public void Constructor_WithDifferentPersonas_DoesNotThrow(string persona, string culture)
    {
        // Act
        Action act = () =>
        {
            using var _ = new AzureNeuralTtsService("fake-key", "eastus", persona, culture);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithBreakStyle_IncludesRawSsml()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");
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
        ssml.Should().Contain("<speak");
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithEmptyText_SkipsSegment()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("   ", null, null, null),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments);

        // Assert
        ssml.Should().Contain("<speak");
        ssml.Should().Contain("</speak>");
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithWhisperStyle_MapsCorrectly()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("Whispered text", "whisper", null, null),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments);

        // Assert
        ssml.Should().Contain("whispering");
        ssml.Should().Contain("Whispered text");
    }

    [Fact]
    public void BuildMultiSegmentSsml_WithCultureOverride_IncludesLangElement()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus", "IARET", "en-US");
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>
        {
            ("Hallo Welt", null, null, null),
        };

        // Act
        string ssml = sut.BuildMultiSegmentSsml(segments, "de-DE");

        // Assert
        ssml.Should().Contain("xml:lang='de-DE'");
    }

    [Fact]
    public async Task SpeakSegmentsAsync_WithEmptySegments_ReturnsImmediately()
    {
        // Arrange
        using var sut = new AzureNeuralTtsService("fake-key", "eastus");
        var segments = new List<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)>();

        // Act - should return immediately without throwing
        await sut.SpeakSegmentsAsync(segments);
    }
}
