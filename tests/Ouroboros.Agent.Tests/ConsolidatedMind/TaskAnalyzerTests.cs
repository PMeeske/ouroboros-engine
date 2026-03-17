// <copyright file="TaskAnalyzerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class TaskAnalyzerTests
{
    // ── Empty / null input ──────────────────────────────────────────────

    [Fact]
    public void Analyze_WithNull_ReturnsQuickResponse()
    {
        // Act
        var result = TaskAnalyzer.Analyze(null!);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
        result.EstimatedComplexity.Should().Be(0.0);
        result.RequiresThinking.Should().BeFalse();
        result.RequiresVerification.Should().BeFalse();
        result.Confidence.Should().Be(1.0);
        result.SecondaryRoles.Should().BeEmpty();
        result.RequiredCapabilities.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_WithEmptyString_ReturnsQuickResponse()
    {
        // Act
        var result = TaskAnalyzer.Analyze(string.Empty);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Analyze_WithWhitespace_ReturnsQuickResponse()
    {
        // Act
        var result = TaskAnalyzer.Analyze("   ");

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
    }

    // ── Code expert routing ─────────────────────────────────────────────

    [Theory]
    [InlineData("Please implement a function that sorts an array")]
    [InlineData("Debug this code and fix the error")]
    [InlineData("Refactor this class to use dependency injection")]
    [InlineData("Write code to parse JSON in C#")]
    public void Analyze_WithCodePrompt_RoutesToCodeExpert(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
    }

    [Fact]
    public void Analyze_WithCodeBlock_RoutesToCodeExpert()
    {
        // Arrange
        var prompt = "Fix this code:\n```csharp\npublic void Test() { }\n```";

        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
    }

    [Fact]
    public void Analyze_WithCodePrompt_RequiresVerification()
    {
        // Act
        var result = TaskAnalyzer.Analyze("implement a function to sort an array");

        // Assert
        result.RequiresVerification.Should().BeTrue();
    }

    // ── Deep reasoning routing ──────────────────────────────────────────

    [Theory]
    [InlineData("Why is the sky blue?")]
    [InlineData("Explain why water boils at 100 degrees")]
    [InlineData("Analyze the implications of quantum computing")]
    public void Analyze_WithReasoningPrompt_RoutesToDeepReasoning(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.DeepReasoning);
        result.RequiresThinking.Should().BeTrue();
    }

    // ── Mathematical routing ────────────────────────────────────────────

    [Theory]
    [InlineData("Calculate 25 + 37 * 2")]
    [InlineData("Solve this equation: x^2 + 3x - 4 = 0")]
    [InlineData("Compute the integral of sin(x)")]
    public void Analyze_WithMathPrompt_RoutesToMathematical(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Mathematical);
        result.RequiresThinking.Should().BeTrue();
        result.RequiresVerification.Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithFrequencyMeasurement_RoutesToMathematical()
    {
        // Act
        var result = TaskAnalyzer.Analyze("What is the effect of a 440 Hz tone?");

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Mathematical);
    }

    // ── Creative routing ────────────────────────────────────────────────

    [Theory]
    [InlineData("Write a short story about a dragon")]
    [InlineData("Create a poem about autumn")]
    [InlineData("Brainstorm ideas for a new product")]
    public void Analyze_WithCreativePrompt_RoutesToCreative(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Creative);
    }

    // ── Planner routing ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Plan the steps to build a web application")]
    [InlineData("Outline a strategy for learning machine learning")]
    [InlineData("Break down the process of deploying to production")]
    public void Analyze_WithPlanningPrompt_RoutesToPlanner(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Planner);
        result.RequiresThinking.Should().BeTrue();
    }

    // ── Synthesizer routing ─────────────────────────────────────────────

    [Theory]
    [InlineData("Summarize this article for me")]
    [InlineData("Give me a brief overview of the main ideas")]
    [InlineData("TLDR of this document")]
    public void Analyze_WithSynthesisPrompt_RoutesToSynthesizer(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Synthesizer);
    }

    // ── Analyst routing ─────────────────────────────────────────────────

    [Fact]
    public void Analyze_WithProsAndCons_RoutesToAnalyst()
    {
        // Act
        var result = TaskAnalyzer.Analyze("What are the pros and cons of microservices?");

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Analyst);
    }

    // ── Verifier routing ────────────────────────────────────────────────

    [Theory]
    [InlineData("Verify this claim is correct")]
    [InlineData("Is it true that the earth is flat? Fact check this")]
    [InlineData("Validate this configuration")]
    public void Analyze_WithVerificationPrompt_RoutesToVerifier(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.Verifier);
    }

    // ── MetaCognitive routing ───────────────────────────────────────────

    [Theory]
    [InlineData("What are your capabilities and abilities?")]
    [InlineData("Describe your features")]
    public void Analyze_WithMetaCognitivePrompt_RoutesToMetaCognitive(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.MetaCognitive);
    }

    // ── Fallback for short unmatched prompts ────────────────────────────

    [Fact]
    public void Analyze_WithShortUnmatchedPrompt_RoutesToQuickResponse()
    {
        // Act
        var result = TaskAnalyzer.Analyze("Hello there");

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
        result.Confidence.Should().Be(0.5);
    }

    [Fact]
    public void Analyze_WithLongUnmatchedPrompt_RoutesToDeepReasoning()
    {
        // Arrange — a 200+ character prompt without matching keywords
        var prompt = new string('a', 250);

        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.PrimaryRole.Should().Be(SpecializedRole.DeepReasoning);
        result.Confidence.Should().Be(0.5);
    }

    // ── Complexity estimation ───────────────────────────────────────────

    [Fact]
    public void Analyze_WithComplexIndicators_HasHighComplexity()
    {
        // Act
        var result = TaskAnalyzer.Analyze("Provide a comprehensive and detailed analysis of the sophisticated algorithms");

        // Assert
        result.EstimatedComplexity.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void Analyze_WithSimpleIndicators_HasLowComplexity()
    {
        // Act
        var result = TaskAnalyzer.Analyze("Give me a simple quick answer");

        // Assert
        result.EstimatedComplexity.Should().BeLessThan(0.3);
    }

    [Fact]
    public void Analyze_WithMultiStepIndicators_IncreasesComplexity()
    {
        // Act
        var result = TaskAnalyzer.Analyze("First do this, then do that, and finally complete it");

        // Assert
        result.EstimatedComplexity.Should().BeGreaterThan(0.1);
    }

    [Fact]
    public void Analyze_WithCodeBlock_IncreasesComplexity()
    {
        // Arrange
        var prompt = "Fix this:\n```csharp\nvar x = 1;\n```";

        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.EstimatedComplexity.Should().BeGreaterThan(0.1);
    }

    // ── Verification triggers ───────────────────────────────────────────

    [Theory]
    [InlineData("This is important for production")]
    [InlineData("This is critical to get right")]
    public void Analyze_WithImportantKeywords_RequiresVerification(string prompt)
    {
        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.RequiresVerification.Should().BeTrue();
    }

    // ── ShouldDecompose ─────────────────────────────────────────────────

    [Fact]
    public void ShouldDecompose_WithHighComplexity_ReturnsTrue()
    {
        // Arrange
        var analysis = new TaskAnalysis(
            SpecializedRole.DeepReasoning,
            Array.Empty<SpecializedRole>(),
            Array.Empty<string>(),
            EstimatedComplexity: 0.8,
            RequiresThinking: true,
            RequiresVerification: false,
            Confidence: 0.9);

        // Act & Assert
        TaskAnalyzer.ShouldDecompose(analysis).Should().BeTrue();
    }

    [Fact]
    public void ShouldDecompose_WithManySecondaryRoles_ReturnsTrue()
    {
        // Arrange
        var analysis = new TaskAnalysis(
            SpecializedRole.Planner,
            new[] { SpecializedRole.CodeExpert, SpecializedRole.Analyst },
            Array.Empty<string>(),
            EstimatedComplexity: 0.3,
            RequiresThinking: false,
            RequiresVerification: false,
            Confidence: 0.7);

        // Act & Assert
        TaskAnalyzer.ShouldDecompose(analysis).Should().BeTrue();
    }

    [Fact]
    public void ShouldDecompose_WithManyCapabilities_ReturnsTrue()
    {
        // Arrange
        var analysis = new TaskAnalysis(
            SpecializedRole.DeepReasoning,
            Array.Empty<SpecializedRole>(),
            new[] { "a", "b", "c", "d", "e", "f" },
            EstimatedComplexity: 0.3,
            RequiresThinking: false,
            RequiresVerification: false,
            Confidence: 0.7);

        // Act & Assert
        TaskAnalyzer.ShouldDecompose(analysis).Should().BeTrue();
    }

    [Fact]
    public void ShouldDecompose_WithSimpleTask_ReturnsFalse()
    {
        // Arrange
        var analysis = new TaskAnalysis(
            SpecializedRole.QuickResponse,
            Array.Empty<SpecializedRole>(),
            new[] { "fast" },
            EstimatedComplexity: 0.2,
            RequiresThinking: false,
            RequiresVerification: false,
            Confidence: 0.9);

        // Act & Assert
        TaskAnalyzer.ShouldDecompose(analysis).Should().BeFalse();
    }

    // ── Secondary roles ─────────────────────────────────────────────────

    [Fact]
    public void Analyze_WithMultiDomainPrompt_IncludesSecondaryRoles()
    {
        // Arrange — a prompt that matches both code and reasoning
        var prompt = "Implement a function and explain why this algorithm works";

        // Act
        var result = TaskAnalyzer.Analyze(prompt);

        // Assert
        result.SecondaryRoles.Should().NotBeEmpty();
    }
}
