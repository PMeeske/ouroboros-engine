// <copyright file="SpeechToTextToolExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.SpeechToText;

using System;
using FluentAssertions;
using Moq;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Tools;
using Xunit;

[Trait("Category", "Unit")]
public class SpeechToTextToolExtensionsTests
{
    [Fact]
    public void WithSpeechToText_WithNullApiKeyAndNoEnvVar_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        string? originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            // Act
            Action act = () => registry.WithSpeechToText(null);

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
    public void WithSpeechToText_WithEmptyApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        string? originalKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            // Act
            Action act = () => registry.WithSpeechToText("");

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalKey);
        }
    }

    [Fact]
    public void WithSpeechToText_WithCustomService_ReturnsRegistryWithTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var mockService = new Mock<ISpeechToTextService>();

        // Act
        var result = registry.WithSpeechToText(mockService.Object);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void WithLocalSpeechToText_ReturnsRegistryWithTool()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.WithLocalSpeechToText("tiny");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void WithLocalSpeechToText_DefaultModelSize_ReturnsRegistryWithTool()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.WithLocalSpeechToText();

        // Assert
        result.Should().NotBeNull();
    }
}
