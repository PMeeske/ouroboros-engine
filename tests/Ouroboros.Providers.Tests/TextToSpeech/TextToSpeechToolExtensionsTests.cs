// <copyright file="TextToSpeechToolExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TextToSpeech;

using System;
using FluentAssertions;
using Moq;
using Ouroboros.Providers;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Tools;
using Xunit;

[Trait("Category", "Unit")]
public class TextToSpeechToolExtensionsTests
{
    [Fact]
    public void WithTextToSpeech_WithNullApiKeyAndNoEnvVar_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        string? originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            // Act
            Action act = () => registry.WithTextToSpeech(null);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*API key*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
    }

    [Fact]
    public void WithTextToSpeech_WithEmptyApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        string? originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            // Act
            Action act = () => registry.WithTextToSpeech("");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
    }

    [Fact]
    public void WithTextToSpeech_WithCustomService_ReturnsRegistryWithTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var mockService = new Mock<ITextToSpeechService>();

        // Act
        var result = registry.WithTextToSpeech(mockService.Object);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void WithBidirectionalSpeech_WithNullApiKeyAndNoEnvVar_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        string? originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            // Act
            Action act = () => registry.WithBidirectionalSpeech(null);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
    }
}
