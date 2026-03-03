// <copyright file="TaskAnalyzerExtendedTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class TaskAnalyzerExtendedTests
{
    // --- Empty / null input ---

    [Fact]
    public void Analyze_EmptyString_ReturnsQuickResponse()
    {
        var result = TaskAnalyzer.Analyze("");
        result.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
        result.EstimatedComplexity.Should().Be(0.0);
        result.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Analyze_WhitespaceOnly_ReturnsQuickResponse()
    {
        var result = TaskAnalyzer.Analyze("   ");
        result.PrimaryRole.Should().Be(SpecializedRole.QuickResponse);
    }

    // --- Code tasks ---

    [Fact]
    public void Analyze_CodeKeywords_ReturnsCodeExpert()
    {
        var result = TaskAnalyzer.Analyze("implement a function to sort an array");
        result.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
    }

    [Fact]
    public void Analyze_DebugTask_ReturnsCodeExpert()
    {
        var result = TaskAnalyzer.Analyze("debug this error in the API endpoint");
        result.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
    }

    [Fact]
    public void Analyze_RefactorTask_ReturnsCodeExpert()
    {
        var result = TaskAnalyzer.Analyze("refactor the authentication module");
        result.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
    }

    [Fact]
    public void Analyze_CodeBlock_ReturnsCodeExpert()
    {
        var result = TaskAnalyzer.Analyze("Fix this:\n```csharp\nvar x = 1;\n```");
        result.PrimaryRole.Should().Be(SpecializedRole.CodeExpert);
        result.RequiresVerification.Should().BeTrue();
    }

    // --- Math tasks ---

    [Fact]
    public void Analyze_MathExpression_ReturnsMathematical()
    {
        var result = TaskAnalyzer.Analyze("calculate 2 + 3 * 4");
        result.PrimaryRole.Should().Be(SpecializedRole.Mathematical);
    }

    [Fact]
    public void Analyze_SolveEquation_ReturnsMathematical()
    {
        var result = TaskAnalyzer.Analyze("solve this equation for x");
        result.PrimaryRole.Should().Be(SpecializedRole.Mathematical);
    }

    [Fact]
    public void Analyze_StatisticalTask_ReturnsMathematical()
    {
        var result = TaskAnalyzer.Analyze("compute the probability of this event");
        result.PrimaryRole.Should().Be(SpecializedRole.Mathematical);
    }

    // --- Reasoning tasks ---

    [Fact]
    public void Analyze_WhyQuestion_ReturnsDeepReasoning()
    {
        var result = TaskAnalyzer.Analyze("why does the sky appear blue?");
        result.PrimaryRole.Should().Be(SpecializedRole.DeepReasoning);
        result.RequiresThinking.Should().BeTrue();
    }

    [Fact]
    public void Analyze_EvaluateTask_ReturnsReasoningOrAnalyst()
    {
        var result = TaskAnalyzer.Analyze("evaluate the impact of climate change");
        var validRoles = new[] { SpecializedRole.DeepReasoning, SpecializedRole.Analyst };
        validRoles.Should().Contain(result.PrimaryRole);
    }

    // --- Creative tasks ---

    [Fact]
    public void Analyze_WriteStory_ReturnsCreative()
    {
        var result = TaskAnalyzer.Analyze("write a story about a brave knight");
        result.PrimaryRole.Should().Be(SpecializedRole.Creative);
    }

    [Fact]
    public void Analyze_BrainstormIdeas_ReturnsCreative()
    {
        var result = TaskAnalyzer.Analyze("brainstorm ideas for a new product");
        result.PrimaryRole.Should().Be(SpecializedRole.Creative);
    }

    // --- Planning tasks ---

    [Fact]
    public void Analyze_PlanTask_ReturnsPlanner()
    {
        var result = TaskAnalyzer.Analyze("plan the steps to build a website");
        result.PrimaryRole.Should().Be(SpecializedRole.Planner);
        result.RequiresThinking.Should().BeTrue();
    }

    [Fact]
    public void Analyze_BreakDown_ReturnsPlanner()
    {
        var result = TaskAnalyzer.Analyze("break down this project into phases");
        result.PrimaryRole.Should().Be(SpecializedRole.Planner);
    }

    // --- Summarization tasks ---

    [Fact]
    public void Analyze_SummarizeTask_ReturnsSynthesizer()
    {
        var result = TaskAnalyzer.Analyze("summarize the key points from the meeting");
        result.PrimaryRole.Should().Be(SpecializedRole.Synthesizer);
    }

    [Fact]
    public void Analyze_TldrTask_ReturnsSynthesizer()
    {
        var result = TaskAnalyzer.Analyze("give me a tldr of this article");
        result.PrimaryRole.Should().Be(SpecializedRole.Synthesizer);
    }

    // --- Verification tasks ---

    [Fact]
    public void Analyze_VerifyTask_ReturnsVerifier()
    {
        var result = TaskAnalyzer.Analyze("verify this claim is accurate");
        result.PrimaryRole.Should().Be(SpecializedRole.Verifier);
    }

    [Fact]
    public void Analyze_FactCheck_ReturnsVerifier()
    {
        var result = TaskAnalyzer.Analyze("is it true that the Earth is flat? fact check");
        result.PrimaryRole.Should().Be(SpecializedRole.Verifier);
    }

    // --- Analyst tasks ---

    [Fact]
    public void Analyze_ProsAndCons_ReturnsAnalyst()
    {
        var result = TaskAnalyzer.Analyze("list the pros and cons of this approach");
        result.PrimaryRole.Should().Be(SpecializedRole.Analyst);
    }

    // --- MetaCognitive tasks ---

    [Fact]
    public void Analyze_SelfImprove_ReturnsMetaCognitive()
    {
        var result = TaskAnalyzer.Analyze("improve yourself to be more helpful");
        // "improve" matches Planner; "yourself" + "improve" matches MetaCognitive
        var validRoles = new[] { SpecializedRole.MetaCognitive, SpecializedRole.Planner };
        validRoles.Should().Contain(result.PrimaryRole);
    }

    [Fact]
    public void Analyze_Capabilities_ReturnsMetaCognitive()
    {
        var result = TaskAnalyzer.Analyze("what are your capabilities and abilities");
        result.PrimaryRole.Should().Be(SpecializedRole.MetaCognitive);
    }

    // --- Complexity estimation ---

    [Fact]
    public void Analyze_ShortSimpleQuestion_LowComplexity()
    {
        var result = TaskAnalyzer.Analyze("what is 2+2?");
        result.EstimatedComplexity.Should().BeLessThan(0.3);
    }

    [Fact]
    public void Analyze_ComplexPrompt_HighComplexity()
    {
        var result = TaskAnalyzer.Analyze(
            "Provide a comprehensive, detailed, and thorough analysis of the " +
            "multi-step process involved in building a sophisticated machine learning " +
            "pipeline. First outline the architecture, then explain each component.");
        result.EstimatedComplexity.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void Analyze_SimpleKeyword_ReducesComplexity()
    {
        var result = TaskAnalyzer.Analyze("give me a simple quick explanation");
        result.EstimatedComplexity.Should().BeLessThan(0.2);
    }

    // --- ShouldDecompose ---

    [Fact]
    public void ShouldDecompose_LowComplexity_ReturnsFalse()
    {
        var analysis = new TaskAnalysis(
            SpecializedRole.QuickResponse, Array.Empty<SpecializedRole>(),
            Array.Empty<string>(), 0.3, false, false, 0.9);

        TaskAnalyzer.ShouldDecompose(analysis).Should().BeFalse();
    }

    [Fact]
    public void ShouldDecompose_HighComplexity_ReturnsTrue()
    {
        var analysis = new TaskAnalysis(
            SpecializedRole.CodeExpert, Array.Empty<SpecializedRole>(),
            Array.Empty<string>(), 0.8, true, true, 0.9);

        TaskAnalyzer.ShouldDecompose(analysis).Should().BeTrue();
    }

    [Fact]
    public void ShouldDecompose_ManySecondaryRoles_ReturnsTrue()
    {
        var analysis = new TaskAnalysis(
            SpecializedRole.CodeExpert,
            new[] { SpecializedRole.Mathematical, SpecializedRole.DeepReasoning },
            Array.Empty<string>(), 0.5, false, false, 0.8);

        TaskAnalyzer.ShouldDecompose(analysis).Should().BeTrue();
    }

    [Fact]
    public void ShouldDecompose_ManyCapabilities_ReturnsTrue()
    {
        var analysis = new TaskAnalysis(
            SpecializedRole.CodeExpert,
            Array.Empty<SpecializedRole>(),
            new[] { "a", "b", "c", "d", "e", "f" },
            0.5, false, false, 0.8);

        TaskAnalyzer.ShouldDecompose(analysis).Should().BeTrue();
    }

    // --- Confidence ---

    [Fact]
    public void Analyze_ClearCodeTask_HighConfidence()
    {
        var result = TaskAnalyzer.Analyze("implement a function to sort an array using quicksort");
        result.Confidence.Should().BeGreaterThan(0.5);
    }

    // --- Secondary roles ---

    [Fact]
    public void Analyze_MixedCodeAndMath_HasSecondaryRoles()
    {
        var result = TaskAnalyzer.Analyze(
            "implement a function to compute the derivative of a polynomial equation " +
            "using calculus and return the result as code");

        result.SecondaryRoles.Should().NotBeEmpty();
    }

    // --- RequiresVerification ---

    [Fact]
    public void Analyze_ProductionKeyword_RequiresVerification()
    {
        var result = TaskAnalyzer.Analyze("write production-ready code for the API");
        result.RequiresVerification.Should().BeTrue();
    }

    [Fact]
    public void Analyze_CriticalKeyword_RequiresVerification()
    {
        var result = TaskAnalyzer.Analyze("this is a critical system component");
        result.RequiresVerification.Should().BeTrue();
    }
}
