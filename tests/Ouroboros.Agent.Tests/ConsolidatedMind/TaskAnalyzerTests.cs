// <copyright file="TaskAnalyzerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public class TaskAnalyzerTests
{
    [Theory]
    [InlineData("Write a function to sort an array", SpecializedRole.CodeExpert)]
    [InlineData("implement a binary search algorithm", SpecializedRole.CodeExpert)]
    [InlineData("debug this error in the code", SpecializedRole.CodeExpert)]
    public void Analyze_CodeTasks_ReturnsCodeExpert(string prompt, SpecializedRole expectedRole)
    {
        var analysis = TaskAnalyzer.Analyze(prompt);
        analysis.PrimaryRole.Should().Be(expectedRole);
    }

    [Theory]
    [InlineData("Calculate 2 + 2 * 3", SpecializedRole.Mathematical)]
    [InlineData("Solve this equation: x^2 + 5x + 6 = 0", SpecializedRole.Mathematical)]
    public void Analyze_MathTasks_ReturnsMathematical(string prompt, SpecializedRole expectedRole)
    {
        var analysis = TaskAnalyzer.Analyze(prompt);
        analysis.PrimaryRole.Should().Be(expectedRole);
    }

    [Theory]
    [InlineData("Write a short story about a dragon")]
    [InlineData("Create a poem about nature")]
    public void Analyze_CreativeTasks_ReturnsCreative(string prompt)
    {
        var analysis = TaskAnalyzer.Analyze(prompt);
        analysis.PrimaryRole.Should().Be(SpecializedRole.Creative);
    }

    [Fact]
    public void Analyze_EmptyPrompt_ReturnsQuickResponse()
    {
        var analysis = TaskAnalyzer.Analyze("");
        analysis.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
    }

    [Fact]
    public void Analyze_SimpleQuestion_ReturnsQuickResponse()
    {
        var analysis = TaskAnalyzer.Analyze("Hello, how are you?");
        analysis.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
    }

    [Fact]
    public void Analyze_ReturnsConfidenceScore()
    {
        var analysis = TaskAnalyzer.Analyze("implement a sorting algorithm in C#");
        analysis.Confidence.Should().BeGreaterThanOrEqualTo(0.0);
        analysis.Confidence.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Analyze_ReturnsCapabilities()
    {
        var analysis = TaskAnalyzer.Analyze("debug this Python function");
        analysis.RequiredCapabilities.Should().NotBeNull();
    }
}
