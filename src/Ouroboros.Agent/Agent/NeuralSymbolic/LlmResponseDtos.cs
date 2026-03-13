// ==========================================================
// Structured LLM Response DTOs
// Type-safe data transfer objects for parsing LLM responses
// ==========================================================

using System.Text.Json.Serialization;

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Structured response for goal decomposition containing a list of subgoals.
/// Replaces regex-based "SUBGOAL N:" parsing in <see cref="MetaAI.GoalHierarchy"/>.
/// </summary>
public sealed record SubgoalResponse(
    /// <summary>The decomposed subgoals.</summary>
    [property: JsonPropertyName("subgoals")]
    IReadOnlyList<SubgoalDto> Subgoals);

/// <summary>
/// A single subgoal produced by LLM-based goal decomposition.
/// </summary>
public sealed record SubgoalDto(
    /// <summary>A clear, one-sentence description of the subgoal.</summary>
    [property: JsonPropertyName("description")]
    string Description,

    /// <summary>The goal type (Primary, Secondary, Instrumental, Safety).</summary>
    [property: JsonPropertyName("type")]
    string Type,

    /// <summary>Priority weight in the range [0.0, 1.0].</summary>
    [property: JsonPropertyName("priority")]
    double Priority);

/// <summary>
/// Structured response for consistency analysis between a hypothesis and a knowledge base.
/// Replaces "CONSISTENT: Yes/No" string matching in <see cref="NeuralSymbolicBridge"/>.
/// </summary>
public sealed record ConsistencyAnalysisDto(
    /// <summary>Whether the hypothesis is logically consistent with the knowledge base.</summary>
    [property: JsonPropertyName("isConsistent")]
    bool IsConsistent,

    /// <summary>Descriptions of logical conflicts found, if any.</summary>
    [property: JsonPropertyName("conflicts")]
    IReadOnlyList<string> Conflicts,

    /// <summary>Prerequisites that are missing from the knowledge base.</summary>
    [property: JsonPropertyName("missingPrerequisites")]
    IReadOnlyList<string> MissingPrerequisites);

/// <summary>
/// Structured response for concept grounding, providing MeTTa type information.
/// Replaces "Type: / Properties: / Relations:" field extraction in <see cref="NeuralSymbolicBridge"/>.
/// </summary>
public sealed record GroundingResponseDto(
    /// <summary>The MeTTa type for the grounded concept.</summary>
    [property: JsonPropertyName("mettaType")]
    string MeTTaType,

    /// <summary>Key properties of the concept (3-5 items).</summary>
    [property: JsonPropertyName("properties")]
    IReadOnlyList<string> Properties,

    /// <summary>Relations to other concepts (2-3 items).</summary>
    [property: JsonPropertyName("relations")]
    IReadOnlyList<string> Relations);

/// <summary>
/// Structured response for value alignment checks.
/// Replaces <c>response.Contains("ALIGNED")</c> checks in <see cref="MetaAI.GoalHierarchy"/>.
/// </summary>
public sealed record AlignmentResponseDto(
    /// <summary>Whether the goal aligns with core values and safety constraints.</summary>
    [property: JsonPropertyName("isAligned")]
    bool IsAligned,

    /// <summary>Explanation of the alignment or misalignment reasoning.</summary>
    [property: JsonPropertyName("explanation")]
    string Explanation);

/// <summary>
/// Structured response for symbolic rule extraction from a learned skill.
/// Replaces "RULE: / METTA: / DESCRIPTION:" block parsing in <see cref="NeuralSymbolicBridge"/>.
/// </summary>
public sealed record RuleExtractionResponseDto(
    /// <summary>The extracted symbolic rules in MeTTa S-expression syntax.</summary>
    [property: JsonPropertyName("rules")]
    IReadOnlyList<ExtractedRuleDto> Rules);

/// <summary>
/// A single rule extracted from a skill by the LLM.
/// </summary>
public sealed record ExtractedRuleDto(
    /// <summary>The rule name.</summary>
    [property: JsonPropertyName("name")]
    string Name,

    /// <summary>MeTTa S-expression representation.</summary>
    [property: JsonPropertyName("metta")]
    string MeTTa,

    /// <summary>Natural language description of what the rule does.</summary>
    [property: JsonPropertyName("description")]
    string Description,

    /// <summary>Preconditions that must hold for the rule to apply.</summary>
    [property: JsonPropertyName("preconditions")]
    IReadOnlyList<string> Preconditions,

    /// <summary>Effects produced when the rule fires.</summary>
    [property: JsonPropertyName("effects")]
    IReadOnlyList<string> Effects);

/// <summary>
/// Structured response for natural language to MeTTa conversion.
/// Replaces S-expression regex extraction in <see cref="NeuralSymbolicBridge"/>.
/// </summary>
public sealed record MeTTaConversionResponseDto(
    /// <summary>The MeTTa S-expression converted from natural language.</summary>
    [property: JsonPropertyName("expression")]
    string Expression);
