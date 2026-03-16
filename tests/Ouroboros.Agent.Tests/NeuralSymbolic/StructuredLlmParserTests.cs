// <copyright file="StructuredLlmParserTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions.Errors;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Tests.NeuralSymbolic;

[Trait("Category", "Unit")]
public class StructuredLlmParserTests
{
    // ================================================================
    // TryParseJson — SubgoalResponse
    // ================================================================

    [Fact]
    public void TryParseJson_ValidSubgoalJson_ReturnsSuccess()
    {
        // Arrange
        var json = """
            {
              "subgoals": [
                { "description": "Gather requirements", "type": "Primary", "priority": 0.9 },
                { "description": "Design architecture", "type": "Instrumental", "priority": 0.7 }
              ]
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<SubgoalResponse>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Subgoals.Should().HaveCount(2);
        result.Value.Subgoals[0].Description.Should().Be("Gather requirements");
        result.Value.Subgoals[0].Type.Should().Be("Primary");
        result.Value.Subgoals[0].Priority.Should().Be(0.9);
        result.Value.Subgoals[1].Description.Should().Be("Design architecture");
    }

    [Fact]
    public void TryParseJson_EmptyString_ReturnsFailureWithLlmParseCode()
    {
        // Arrange & Act
        var result = StructuredLlmParser.TryParseJson<SubgoalResponse>("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
        result.Error.Message.Should().Contain("empty");
    }

    [Fact]
    public void TryParseJson_NullString_ReturnsFailure()
    {
        // Arrange & Act
        var result = StructuredLlmParser.TryParseJson<SubgoalResponse>(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
    }

    [Fact]
    public void TryParseJson_WhitespaceOnly_ReturnsFailure()
    {
        // Arrange & Act
        var result = StructuredLlmParser.TryParseJson<SubgoalResponse>("   \n\t  ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
    }

    [Fact]
    public void TryParseJson_PlainTextNoJson_ReturnsFailure()
    {
        // Arrange
        var plainText = "SUBGOAL 1: Gather requirements\nTYPE: Primary\nPRIORITY: 0.9";

        // Act
        var result = StructuredLlmParser.TryParseJson<SubgoalResponse>(plainText);

        // Assert — the text does not contain a JSON object or array
        // so extraction should fail. Note: this particular text does not
        // contain braces/brackets, so we expect failure.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
    }

    [Fact]
    public void TryParseJson_MalformedJson_ReturnsFailureWithException()
    {
        // Arrange
        var badJson = """{ "subgoals": [ { "description": "missing closing""" + " }";

        // Act
        var result = StructuredLlmParser.TryParseJson<SubgoalResponse>(badJson);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
        result.Error.InnerException.Should().NotBeNull();
    }

    // ================================================================
    // TryParseJson — ConsistencyAnalysisDto
    // ================================================================

    [Fact]
    public void TryParseJson_ValidConsistencyJson_ReturnsSuccess()
    {
        // Arrange
        var json = """
            {
              "isConsistent": false,
              "conflicts": ["Rule A contradicts Rule B"],
              "missingPrerequisites": ["Domain knowledge about X"]
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<ConsistencyAnalysisDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsConsistent.Should().BeFalse();
        result.Value.Conflicts.Should().ContainSingle("Rule A contradicts Rule B");
        result.Value.MissingPrerequisites.Should().ContainSingle("Domain knowledge about X");
    }

    [Fact]
    public void TryParseJson_ConsistentResult_ReturnsEmptyLists()
    {
        // Arrange
        var json = """
            {
              "isConsistent": true,
              "conflicts": [],
              "missingPrerequisites": []
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<ConsistencyAnalysisDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsConsistent.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
        result.Value.MissingPrerequisites.Should().BeEmpty();
    }

    // ================================================================
    // TryParseJson — GroundingResponseDto
    // ================================================================

    [Fact]
    public void TryParseJson_ValidGroundingJson_ReturnsSuccess()
    {
        // Arrange
        var json = """
            {
              "mettaType": "Entity",
              "properties": ["sentient", "mobile", "autonomous"],
              "relations": ["is-a Agent", "has-capability Reasoning"]
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<GroundingResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MeTTaType.Should().Be("Entity");
        result.Value.Properties.Should().HaveCount(3);
        result.Value.Relations.Should().HaveCount(2);
    }

    // ================================================================
    // TryParseJson — AlignmentResponseDto
    // ================================================================

    [Fact]
    public void TryParseJson_AlignedResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """
            {
              "isAligned": true,
              "explanation": "The goal aligns with core safety values."
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeTrue();
        result.Value.Explanation.Should().Contain("safety");
    }

    [Fact]
    public void TryParseJson_MisalignedResponse_ReturnsSuccess()
    {
        // Arrange
        var json = """
            {
              "isAligned": false,
              "explanation": "The goal conflicts with the non-harm principle."
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeFalse();
    }

    // ================================================================
    // TryParseJson — RuleExtractionResponseDto
    // ================================================================

    [Fact]
    public void TryParseJson_ValidRuleExtractionJson_ReturnsSuccess()
    {
        // Arrange
        var json = """
            {
              "rules": [
                {
                  "name": "navigate-to-goal",
                  "metta": "(= (navigate $agent $dest) (move $agent (path-to $dest)))",
                  "description": "Agent navigates to a destination",
                  "preconditions": ["agent-exists", "destination-reachable"],
                  "effects": ["agent-at-destination"]
                }
              ]
            }
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<RuleExtractionResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rules.Should().ContainSingle();
        result.Value.Rules[0].Name.Should().Be("navigate-to-goal");
        result.Value.Rules[0].MeTTa.Should().Contain("navigate");
        result.Value.Rules[0].Preconditions.Should().HaveCount(2);
        result.Value.Rules[0].Effects.Should().ContainSingle();
    }

    // ================================================================
    // TryParseJson — MeTTaConversionResponseDto
    // ================================================================

    [Fact]
    public void TryParseJson_ValidMeTTaConversionJson_ReturnsSuccess()
    {
        // Arrange
        var json = """{ "expression": "(= (is-cat $x) (has-fur $x))" }""";

        // Act
        var result = StructuredLlmParser.TryParseJson<MeTTaConversionResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Expression.Should().Be("(= (is-cat $x) (has-fur $x))");
    }

    // ================================================================
    // JSON extraction from wrapped content (markdown fences, preamble)
    // Tests the internal ExtractJsonBlock logic via the public TryParseJson API
    // ================================================================

    [Fact]
    public void TryParseJson_MarkdownFencedJson_ExtractsAndParses()
    {
        // Arrange
        var input = """
            Here is the result:
            ```json
            { "isAligned": true, "explanation": "Looks good" }
            ```
            Hope that helps!
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeTrue();
        result.Value.Explanation.Should().Be("Looks good");
    }

    [Fact]
    public void TryParseJson_MarkdownFenceWithoutJsonLabel_ExtractsAndParses()
    {
        // Arrange
        var input = """
            ```
            { "expression": "(foo $x)" }
            ```
            """;

        // Act
        var result = StructuredLlmParser.TryParseJson<MeTTaConversionResponseDto>(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Expression.Should().Be("(foo $x)");
    }

    [Fact]
    public void TryParseJson_BareJsonObjectWithSurroundingText_ExtractsAndParses()
    {
        // Arrange
        var input = """Some preamble { "mettaType": "Concept", "properties": ["a"], "relations": ["b"] } trailing text""";

        // Act
        var result = StructuredLlmParser.TryParseJson<GroundingResponseDto>(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MeTTaType.Should().Be("Concept");
    }

    [Fact]
    public void TryParseJson_NoJsonPresent_ReturnsFailure()
    {
        // Arrange
        var input = "This is just plain text with no JSON at all.";

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
    }

    // ================================================================
    // ParseWithFallback
    // ================================================================

    [Fact]
    public void ParseWithFallback_ValidJson_ReturnsJsonResult()
    {
        // Arrange
        var json = """{ "isAligned": true, "explanation": "OK" }""";
        var fallbackCalled = false;

        // Act
        var result = StructuredLlmParser.ParseWithFallback<AlignmentResponseDto>(
            json,
            _ => { fallbackCalled = true; return null; });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeTrue();
        fallbackCalled.Should().BeFalse();
    }

    [Fact]
    public void ParseWithFallback_InvalidJson_UsesFallback()
    {
        // Arrange
        var rawText = "ALIGNED: The goal is safe.";
        var fallback = new AlignmentResponseDto(true, "The goal is safe.");

        // Act
        var result = StructuredLlmParser.ParseWithFallback<AlignmentResponseDto>(
            rawText,
            _ => fallback);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeTrue();
        result.Value.Explanation.Should().Be("The goal is safe.");
    }

    [Fact]
    public void ParseWithFallback_InvalidJson_FallbackReturnsNull_ReturnsFailure()
    {
        // Arrange
        var rawText = "Unstructured text that nothing can parse.";

        // Act
        var result = StructuredLlmParser.ParseWithFallback<AlignmentResponseDto>(
            rawText,
            _ => null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
        result.Error.Message.Should().Contain("Both JSON and fallback");
    }

    [Fact]
    public void ParseWithFallback_InvalidJson_FallbackThrows_ReturnsFailureWithException()
    {
        // Arrange
        var rawText = "Some text";

        // Act
        var result = StructuredLlmParser.ParseWithFallback<AlignmentResponseDto>(
            rawText,
            _ => throw new FormatException("bad format"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
        result.Error.Message.Should().Contain("Fallback parser threw");
        result.Error.InnerException.Should().BeOfType<FormatException>();
    }

    [Fact]
    public void ParseWithFallback_EmptyString_ReturnsFailure()
    {
        // Act
        var result = StructuredLlmParser.ParseWithFallback<AlignmentResponseDto>(
            "",
            _ => new AlignmentResponseDto(true, "should not reach"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.LlmParseFailure);
    }

    [Fact]
    public void ParseWithFallback_NullFallback_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => StructuredLlmParser.ParseWithFallback<AlignmentResponseDto>(
            "text",
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ================================================================
    // BuildJsonSchemaInstruction
    // ================================================================

    [Fact]
    public void BuildJsonSchemaInstruction_SubgoalResponse_ContainsSchemaFields()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<SubgoalResponse>();

        // Assert
        instruction.Should().Contain("subgoals");
        instruction.Should().Contain("description");
        instruction.Should().Contain("type");
        instruction.Should().Contain("priority");
        instruction.Should().Contain("Respond ONLY with valid JSON");
    }

    [Fact]
    public void BuildJsonSchemaInstruction_ConsistencyAnalysis_ContainsSchemaFields()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<ConsistencyAnalysisDto>();

        // Assert
        instruction.Should().Contain("isConsistent");
        instruction.Should().Contain("conflicts");
        instruction.Should().Contain("missingPrerequisites");
    }

    [Fact]
    public void BuildJsonSchemaInstruction_GroundingResponse_ContainsSchemaFields()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<GroundingResponseDto>();

        // Assert
        instruction.Should().Contain("mettaType");
        instruction.Should().Contain("properties");
        instruction.Should().Contain("relations");
    }

    [Fact]
    public void BuildJsonSchemaInstruction_AlignmentResponse_ContainsSchemaFields()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<AlignmentResponseDto>();

        // Assert
        instruction.Should().Contain("isAligned");
        instruction.Should().Contain("explanation");
    }

    [Fact]
    public void BuildJsonSchemaInstruction_RuleExtraction_ContainsSchemaFields()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<RuleExtractionResponseDto>();

        // Assert
        instruction.Should().Contain("rules");
        instruction.Should().Contain("name");
        instruction.Should().Contain("metta");
        instruction.Should().Contain("preconditions");
        instruction.Should().Contain("effects");
    }

    [Fact]
    public void BuildJsonSchemaInstruction_MeTTaConversion_ContainsSchemaFields()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<MeTTaConversionResponseDto>();

        // Assert
        instruction.Should().Contain("expression");
    }

    [Fact]
    public void BuildJsonSchemaInstruction_UnknownType_ReturnsFallbackSchema()
    {
        // Act
        var instruction = StructuredLlmParser.BuildJsonSchemaInstruction<object>();

        // Assert
        instruction.Should().Contain("JSON object");
        instruction.Should().Contain("Respond ONLY with valid JSON");
    }

    // ================================================================
    // TryParseJson — case insensitivity and trailing commas
    // ================================================================

    [Fact]
    public void TryParseJson_CaseInsensitivePropertyNames_ReturnsSuccess()
    {
        // Arrange — uppercase property names
        var json = """{ "IsAligned": true, "Explanation": "Case test" }""";

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeTrue();
    }

    [Fact]
    public void TryParseJson_TrailingCommas_ReturnsSuccess()
    {
        // Arrange — trailing commas in JSON (common LLM mistake)
        var json = """{ "isAligned": false, "explanation": "Trailing comma test", }""";

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeFalse();
    }

    // ================================================================
    // DTO record semantics
    // ================================================================

    [Fact]
    public void SubgoalDto_RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new SubgoalDto("desc", "Primary", 0.8);
        var b = new SubgoalDto("desc", "Primary", 0.8);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void ConsistencyAnalysisDto_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var dto = new ConsistencyAnalysisDto(true, [], []);

        // Assert
        dto.IsConsistent.Should().BeTrue();
        dto.Conflicts.Should().BeEmpty();
        dto.MissingPrerequisites.Should().BeEmpty();
    }

    [Fact]
    public void GroundingResponseDto_AllPropertiesAccessible()
    {
        // Arrange
        var dto = new GroundingResponseDto("Concept", ["prop1", "prop2"], ["rel1"]);

        // Assert
        dto.MeTTaType.Should().Be("Concept");
        dto.Properties.Should().HaveCount(2);
        dto.Relations.Should().ContainSingle();
    }

    [Fact]
    public void AlignmentResponseDto_AllPropertiesAccessible()
    {
        // Arrange
        var dto = new AlignmentResponseDto(false, "Misaligned because of X");

        // Assert
        dto.IsAligned.Should().BeFalse();
        dto.Explanation.Should().Contain("Misaligned");
    }

    [Fact]
    public void RuleExtractionResponseDto_AllPropertiesAccessible()
    {
        // Arrange
        var rule = new ExtractedRuleDto("r1", "(r1)", "Rule one", ["pre1"], ["eff1"]);
        var dto = new RuleExtractionResponseDto([rule]);

        // Assert
        dto.Rules.Should().ContainSingle();
        dto.Rules[0].Name.Should().Be("r1");
        dto.Rules[0].MeTTa.Should().Be("(r1)");
        dto.Rules[0].Preconditions.Should().ContainSingle("pre1");
        dto.Rules[0].Effects.Should().ContainSingle("eff1");
    }

    [Fact]
    public void MeTTaConversionResponseDto_AllPropertiesAccessible()
    {
        // Arrange
        var dto = new MeTTaConversionResponseDto("(= (foo) (bar))");

        // Assert
        dto.Expression.Should().Be("(= (foo) (bar))");
    }

    [Fact]
    public void SubgoalResponse_AllPropertiesAccessible()
    {
        // Arrange
        var subgoals = new List<SubgoalDto>
        {
            new("Step 1", "Primary", 0.9),
            new("Step 2", "Safety", 1.0),
        };
        var dto = new SubgoalResponse(subgoals);

        // Assert
        dto.Subgoals.Should().HaveCount(2);
        dto.Subgoals[0].Description.Should().Be("Step 1");
        dto.Subgoals[1].Priority.Should().Be(1.0);
    }

    [Fact]
    public void ExtractedRuleDto_RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new ExtractedRuleDto("r", "(r)", "desc", ["p"], ["e"]);
        var b = new ExtractedRuleDto("r", "(r)", "desc", ["p"], ["e"]);

        // Assert
        a.Should().BeEquivalentTo(b);
    }

    // ================================================================
    // ExtractJsonBlock — balanced-scanner edge cases
    // ================================================================

    [Fact]
    public void ExtractJsonBlock_MultipleJsonBlocks_ReturnsFirstValid()
    {
        // Arrange: two valid JSON objects; old greedy regex would span both
        var input = """some text { "isAligned": true, "explanation": "first" } extra { "isAligned": false, "explanation": "second" }""";

        // Act
        var block = StructuredLlmParser.ExtractJsonBlock(input);

        // Assert: balanced scanner returns the first valid block only
        block.Should().NotBeNull();
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(block!);
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeTrue();
        result.Value.Explanation.Should().Be("first");
    }

    [Fact]
    public void ExtractJsonBlock_JsonContainingEscapedBraces_ReturnsCorrectBlock()
    {
        // Arrange: a JSON string value that itself contains braces
        var input = """{ "expression": "{ nested braces }" }""";

        // Act
        var block = StructuredLlmParser.ExtractJsonBlock(input);

        // Assert
        block.Should().NotBeNull();
        var result = StructuredLlmParser.TryParseJson<MeTTaConversionResponseDto>(block!);
        result.IsSuccess.Should().BeTrue();
        result.Value.Expression.Should().Be("{ nested braces }");
    }

    [Fact]
    public void ExtractJsonBlock_ExtraBracesBeforeValidJson_ReturnsValidBlock()
    {
        // Arrange: stray opening brace before the actual JSON object
        var input = """some { broken stuff } and then { "isAligned": false, "explanation": "ok" }""";

        // Act
        var result = StructuredLlmParser.TryParseJson<AlignmentResponseDto>(input);

        // Assert: parser skips invalid blocks and finds the first parseable one
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAligned.Should().BeFalse();
        result.Value.Explanation.Should().Be("ok");
    }
}
