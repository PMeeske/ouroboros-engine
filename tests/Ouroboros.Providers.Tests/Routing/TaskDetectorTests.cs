// <copyright file="TaskDetectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers.Routing;

/// <summary>
/// Unit tests for <see cref="TaskDetector"/> class.
/// Validates task type detection from various prompt types across all strategies.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskDetectorTests
{
    // ──────────────────────────────────────────────
    // Null / empty / whitespace
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithEmptyPrompt_ReturnsSimple()
    {
        // Arrange
        string prompt = "";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_WithNullPrompt_ReturnsSimple()
    {
        // Arrange
        string? prompt = null;

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt!);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_WithWhitespacePrompt_ReturnsSimple()
    {
        // Arrange
        string prompt = "   \t\n  ";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    // ──────────────────────────────────────────────
    // Heuristic strategy: code detection
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithCodeBlock_ReturnsCoding()
    {
        // Arrange
        string prompt = "Fix this code:\n```python\ndef hello():\n    print('Hello World')\n```";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Theory]
    [InlineData("def quicksort(arr):")]
    [InlineData("class MyService:")]
    [InlineData("function greet(name) { }")]
    [InlineData("const MAX_SIZE = 100;")]
    [InlineData("var result = compute();")]
    [InlineData("let counter = 0;")]
    [InlineData("import numpy as np")]
    [InlineData("public class UserController")]
    [InlineData("private int _count;")]
    public void DetectTaskType_WithProgrammingSyntax_ReturnsCoding(string prompt)
    {
        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_WithImplementRequest_ReturnsCoding()
    {
        // Arrange
        string prompt = "Implement a REST API endpoint for user authentication.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_WithCodingKeywords_ReturnsCoding()
    {
        // Arrange
        string prompt = "Write a function in Python that implements quicksort algorithm.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    // ──────────────────────────────────────────────
    // Heuristic strategy: reasoning detection
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithReasoningPrompt_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Explain why neural networks are effective for pattern recognition.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithWhyQuestion_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Why does water boil at lower temperatures at higher altitudes?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithAnalyzeRequest_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Analyze the impact of climate change on agriculture.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Theory]
    [InlineData("Evaluate whether this approach is sound and the logic holds.")]
    [InlineData("Deduce the consequence of this theorem.")]
    [InlineData("Consider the inference we can draw from these observations.")]
    public void DetectTaskType_WithVariousReasoningKeywords_ReturnsReasoning(string prompt)
    {
        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    // ──────────────────────────────────────────────
    // Heuristic strategy: planning detection
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithPlanningPrompt_ReturnsPlanning()
    {
        // Arrange
        string prompt = "Create a step-by-step plan for implementing a machine learning pipeline.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_WithHowToQuestion_ReturnsPlanning()
    {
        // Arrange
        string prompt = "How to build a scalable microservices architecture?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Theory]
    [InlineData("Outline the approach for this project and decompose it into tasks.")]
    [InlineData("Schedule a roadmap with a clear workflow and procedure.")]
    [InlineData("Design the framework and coordinate the process steps.")]
    public void DetectTaskType_WithVariousPlanningKeywords_ReturnsPlanning(string prompt)
    {
        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    // ──────────────────────────────────────────────
    // Heuristic strategy: simple / unknown
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithSimplePrompt_ReturnsSimple()
    {
        // Arrange
        string prompt = "Hello, how are you?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_WithShortNoKeywords_ReturnsSimple()
    {
        // Arrange
        string prompt = "Good morning.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_WithLongTextNoKeywords_ReturnsUnknown()
    {
        // Arrange - 600 chars of 'x', no keywords
        string prompt = new string('x', 600);

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Unknown);
    }

    // ──────────────────────────────────────────────
    // RuleBased strategy
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_RuleBased_CodeBlock_ReturnsCoding()
    {
        // Arrange
        string prompt = "```\nconsole.log('hi');\n```";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_RuleBased_ProgrammingSyntax_ReturnsCoding()
    {
        // Arrange
        string prompt = "void Main() { return; }";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_RuleBased_ListStructure_ReturnsPlanning()
    {
        // Arrange
        string prompt = "Here is the breakdown:\n1. First step\n2. Second step\n3. Third step";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_RuleBased_BulletList_ReturnsPlanning()
    {
        // Arrange
        string prompt = "Tasks:\n- Do the first thing\n- Do the second thing";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_RuleBased_StartsWithWhy_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Why does the sun rise in the east?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_RuleBased_ContainsExplain_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Please explain the theory of relativity in simple terms.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_RuleBased_ContainsAnalyze_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Could you analyze this dataset for trends?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_RuleBased_ShortSimpleQuery_ReturnsSimple()
    {
        // Arrange
        string prompt = "What time is it?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_RuleBased_LongComplexNoPatterns_ReturnsUnknown()
    {
        // Arrange - long text with newlines but no matching patterns
        string prompt = "This is a very long paragraph that goes on and on.\nIt has multiple lines but no keywords or structure.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Unknown);
    }

    [Fact]
    public void DetectTaskType_RuleBased_ContainsStepKeyword_ReturnsPlanning()
    {
        // Arrange
        string prompt = "What are the steps to deploy a web application?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    // ──────────────────────────────────────────────
    // Hybrid strategy
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(TaskDetectionStrategy.Heuristic)]
    [InlineData(TaskDetectionStrategy.Hybrid)]
    public void DetectTaskType_WithDifferentStrategies_WorksCorrectly(TaskDetectionStrategy strategy)
    {
        // Arrange
        string prompt = "Explain the reasoning behind quantum mechanics.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, strategy);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_Hybrid_CodeBlock_ReturnsCoding()
    {
        // Arrange - both heuristic and rule-based agree on coding
        string prompt = "```\nprint('hello')\n```";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.Hybrid);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_Hybrid_EmptyPrompt_ReturnsSimple()
    {
        // Arrange
        string prompt = "";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.Hybrid);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_Hybrid_CombinesHeuristicsAndRules()
    {
        // Arrange - list structure makes RuleBased return Planning,
        // "plan" and "steps" keywords make Heuristic return Planning too
        string prompt = "Plan the steps:\n1. First\n2. Second\n3. Third";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.Hybrid);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_Hybrid_PrefersHeuristic_WhenBothDiffer()
    {
        // Arrange - heuristic detects reasoning keywords; rule-based may detect simple
        // "Evaluate" is a reasoning keyword for heuristic but not a rule-based trigger
        string prompt = "Evaluate this quickly.";

        // Act
        TaskType heuristicResult = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.Heuristic);
        TaskType hybridResult = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.Hybrid);

        // Assert - hybrid should prefer heuristic when both disagree and neither is Unknown
        hybridResult.Should().Be(heuristicResult);
    }

    // ──────────────────────────────────────────────
    // Mixed keywords
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithMultipleKeywords_SelectsMostRelevant()
    {
        // Arrange - Has both reasoning and planning keywords
        string prompt = "Plan the steps to analyze and decompose a complex reasoning problem.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().BeOneOf(TaskType.Planning, TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithListStructureInHeuristic_ReturnsPlanning()
    {
        // Arrange
        string prompt = "Create a plan with these steps:\n1. Requirements gathering\n2. System design\n3. Implementation\n4. Testing";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    // ──────────────────────────────────────────────
    // Invalid strategy enum value
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_WithInvalidStrategy_ReturnsUnknown()
    {
        // Arrange
        string prompt = "Some prompt text";
        var invalidStrategy = (TaskDetectionStrategy)999;

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, invalidStrategy);

        // Assert
        result.Should().Be(TaskType.Unknown);
    }

    // ──────────────────────────────────────────────
    // Default strategy parameter
    // ──────────────────────────────────────────────

    [Fact]
    public void DetectTaskType_DefaultStrategy_IsHeuristic()
    {
        // Arrange
        string prompt = "Explain the reasoning behind this design.";

        // Act
        TaskType defaultResult = TaskDetector.DetectTaskType(prompt);
        TaskType heuristicResult = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.Heuristic);

        // Assert
        defaultResult.Should().Be(heuristicResult);
    }
}
