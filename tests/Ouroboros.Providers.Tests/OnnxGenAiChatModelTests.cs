// <copyright file="OnnxGenAiChatModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Tests;

using FluentAssertions;
using Microsoft.Extensions.AI;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Integration and unit tests for <see cref="OnnxGenAiChatModel"/>.
/// 
/// ⚠️ These tests require a real ONNX GenAI model to be present.
/// Set <c>OUROBOROS_ONNX_MODEL_PATH</c> before running, or skip with
/// <c>[Trait("Category", "Integration")]</c>.
/// </summary>
public class OnnxGenAiChatModelTests
{
    private static string? GetTestModelPath()
    {
        string? envPath = Environment.GetEnvironmentVariable("OUROBOROS_ONNX_MODEL_PATH");
        return !string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath)
            ? envPath
            : null;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithInvalidPath_ThrowsDirectoryNotFound()
    {
        // Act & Assert
        Action act = () => _ = new OnnxGenAiChatModel("/nonexistent/path");
        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*ONNX model directory not found*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithNullPath_ThrowsArgumentException()
    {
        Action act = () => _ = new OnnxGenAiChatModel(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnnxRuntimeSettings_Default_CreatesConservativeDefaults()
    {
        var settings = OnnxRuntimeSettings.Default;

        settings.Temperature.Should().Be(0.7f);
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.MaxLength.Should().Be(4096);
        settings.RepetitionPenalty.Should().Be(1.1f);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnnxExecutionProviderFactory_CreateBest_ReturnsProvider()
    {
        var provider = OnnxExecutionProviderFactory.CreateBest();

        provider.Should().NotBeNull();
        provider.Name.Should().BeOneOf("DirectML", "CPU");
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateTextAsync_WithValidModel_ReturnsNonEmpty()
    {
        string? modelPath = GetTestModelPath();
        Skip.If(modelPath is null, "OUROBOROS_ONNX_MODEL_PATH not set or directory missing");

        using var model = new OnnxGenAiChatModel(
            modelPath,
            OnnxRuntimeSettings.Deterministic,  // Reproducible output
            executionProvider: OnnxExecutionProviderFactory.CreateBest());

        string response = await model.GenerateTextAsync("What is 2+2?");

        response.Should().NotBeNullOrWhiteSpace();
        response.Should().ContainAny("4", "four");
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GetResponseAsync_WithChatMessages_ReturnsAssistantMessage()
    {
        string? modelPath = GetTestModelPath();
        Skip.If(modelPath is null, "OUROBOROS_ONNX_MODEL_PATH not set or directory missing");

        using var model = new OnnxGenAiChatModel(modelPath);

        var response = await model.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Say 'hello' exactly.")],
            new ChatOptions { MaxOutputTokens = 32 });

        response.Should().NotBeNull();
        response.Message.Text.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GetStreamingResponseAsync_YieldsTokens()
    {
        string? modelPath = GetTestModelPath();
        Skip.If(modelPath is null, "OUROBOROS_ONNX_MODEL_PATH not set or directory missing");

        using var model = new OnnxGenAiChatModel(modelPath);

        var tokens = new List<ChatResponseUpdate>();
        await foreach (var update in model.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "One word: hello")],
            new ChatOptions { MaxOutputTokens = 8 }))
        {
            tokens.Add(update);
        }

        tokens.Should().NotBeEmpty();
        tokens.Should().OnlyContain(t => t.Role == ChatRole.Assistant);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComposePrompt_WithSystemMessage_PrependsSystemHeader()
    {
        // Uses reflection to test private method (or make internal + InternalsVisibleTo)
        // For now: test indirectly via GenerateTextAsync with system prompt override.
        // TODO: add internals access or make ComposePrompt internal
    }
}
