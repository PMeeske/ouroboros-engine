// <copyright file="LlmResponseDtosTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Agent.Tests.NeuralSymbolic;

/// <summary>
/// Unit tests for LLM response DTO records in <see cref="SubgoalResponse"/>,
/// <see cref="ConsistencyAnalysisDto"/>, <see cref="GroundingResponseDto"/>,
/// <see cref="AlignmentResponseDto"/>, <see cref="RuleExtractionResponseDto"/>,
/// and <see cref="MeTTaConversionResponseDto"/>.
/// </summary>
[Trait("Category", "Unit")]
public class LlmResponseDtosTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // --- SubgoalResponse ---

    [Fact]
    public void SubgoalResponse_Roundtrip_PreservesData()
    {
        // Arrange
        var dto = new SubgoalResponse(
            [new SubgoalDto("Implement auth", "Primary", 0.9)]);

        // Act
        string json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<SubgoalResponse>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Subgoals.Should().HaveCount(1);
        deserialized.Subgoals[0].Description.Should().Be("Implement auth");
        deserialized.Subgoals[0].Type.Should().Be("Primary");
        deserialized.Subgoals[0].Priority.Should().Be(0.9);
    }

    [Fact]
    public void SubgoalDto_SetsProperties()
    {
        var dto = new SubgoalDto("Build feature", "Instrumental", 0.7);

        dto.Description.Should().Be("Build feature");
        dto.Type.Should().Be("Instrumental");
        dto.Priority.Should().Be(0.7);
    }

    [Fact]
    public void SubgoalResponse_DeserializesFromCamelCaseJson()
    {
        // Arrange
        const string json = """
        {
            "subgoals": [
                {"description": "Step 1", "type": "Safety", "priority": 1.0},
                {"description": "Step 2", "type": "Secondary", "priority": 0.5}
            ]
        }
        """;

        // Act
        var dto = JsonSerializer.Deserialize<SubgoalResponse>(json, s_options);

        // Assert
        dto.Should().NotBeNull();
        dto!.Subgoals.Should().HaveCount(2);
        dto.Subgoals[0].Type.Should().Be("Safety");
        dto.Subgoals[1].Priority.Should().Be(0.5);
    }

    // --- ConsistencyAnalysisDto ---

    [Fact]
    public void ConsistencyAnalysisDto_Roundtrip_PreservesData()
    {
        // Arrange
        var dto = new ConsistencyAnalysisDto(
            true,
            new List<string>(),
            new List<string> { "missing-prereq" });

        // Act
        string json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<ConsistencyAnalysisDto>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsConsistent.Should().BeTrue();
        deserialized.Conflicts.Should().BeEmpty();
        deserialized.MissingPrerequisites.Should().ContainSingle().Which.Should().Be("missing-prereq");
    }

    [Fact]
    public void ConsistencyAnalysisDto_WithConflicts_DeserializesCorrectly()
    {
        // Arrange
        const string json = """
        {
            "isConsistent": false,
            "conflicts": ["Contradicts rule A", "Violates constraint B"],
            "missingPrerequisites": []
        }
        """;

        // Act
        var dto = JsonSerializer.Deserialize<ConsistencyAnalysisDto>(json, s_options);

        // Assert
        dto.Should().NotBeNull();
        dto!.IsConsistent.Should().BeFalse();
        dto.Conflicts.Should().HaveCount(2);
    }

    // --- GroundingResponseDto ---

    [Fact]
    public void GroundingResponseDto_Roundtrip_PreservesData()
    {
        // Arrange
        var dto = new GroundingResponseDto(
            "Entity",
            new List<string> { "name", "type" },
            new List<string> { "isA", "partOf" });

        // Act
        string json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<GroundingResponseDto>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.MeTTaType.Should().Be("Entity");
        deserialized.Properties.Should().HaveCount(2);
        deserialized.Relations.Should().HaveCount(2);
    }

    // --- AlignmentResponseDto ---

    [Fact]
    public void AlignmentResponseDto_WhenAligned_SetsProperties()
    {
        var dto = new AlignmentResponseDto(true, "Goal is safe and beneficial");

        dto.IsAligned.Should().BeTrue();
        dto.Explanation.Should().Be("Goal is safe and beneficial");
    }

    [Fact]
    public void AlignmentResponseDto_Roundtrip_PreservesData()
    {
        // Arrange
        var dto = new AlignmentResponseDto(false, "Misaligned with safety constraints");

        // Act
        string json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<AlignmentResponseDto>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsAligned.Should().BeFalse();
        deserialized.Explanation.Should().Contain("safety constraints");
    }

    // --- RuleExtractionResponseDto ---

    [Fact]
    public void RuleExtractionResponseDto_Roundtrip_PreservesData()
    {
        // Arrange
        var rule = new ExtractedRuleDto(
            "rule1",
            "(= (rule1 $x) (result $x))",
            "Maps input to result",
            new List<string> { "input exists" },
            new List<string> { "output produced" });

        var dto = new RuleExtractionResponseDto([rule]);

        // Act
        string json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<RuleExtractionResponseDto>(json, s_options);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Rules.Should().HaveCount(1);
        deserialized.Rules[0].Name.Should().Be("rule1");
        deserialized.Rules[0].MeTTa.Should().Contain("rule1");
        deserialized.Rules[0].Preconditions.Should().HaveCount(1);
        deserialized.Rules[0].Effects.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractedRuleDto_SetsAllProperties()
    {
        var dto = new ExtractedRuleDto(
            "test-rule",
            "(= (test $x) $x)",
            "Identity rule",
            new List<string> { "pre1", "pre2" },
            new List<string> { "eff1" });

        dto.Name.Should().Be("test-rule");
        dto.MeTTa.Should().Be("(= (test $x) $x)");
        dto.Description.Should().Be("Identity rule");
        dto.Preconditions.Should().HaveCount(2);
        dto.Effects.Should().HaveCount(1);
    }

    // --- MeTTaConversionResponseDto ---

    [Fact]
    public void MeTTaConversionResponseDto_SetsExpression()
    {
        var dto = new MeTTaConversionResponseDto("(IsA cat animal)");

        dto.Expression.Should().Be("(IsA cat animal)");
    }

    [Fact]
    public void MeTTaConversionResponseDto_Roundtrip_PreservesData()
    {
        // Arrange
        const string json = """{"expression": "(HasProperty dog loyal)"}""";

        // Act
        var dto = JsonSerializer.Deserialize<MeTTaConversionResponseDto>(json, s_options);

        // Assert
        dto.Should().NotBeNull();
        dto!.Expression.Should().Be("(HasProperty dog loyal)");
    }
}
