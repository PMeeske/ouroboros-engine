// ==========================================================
// Structured LLM Response Parser
// JSON-first deserialization with string-parsing fallback
// ==========================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using Ouroboros.Abstractions.Errors;

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Parses raw LLM text output into strongly typed DTOs.
/// Attempts JSON deserialization first, then falls back to legacy string parsing
/// to provide a graceful degradation path during the migration from unstructured
/// to structured LLM responses.
/// </summary>
public static partial class StructuredLlmParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Attempts to deserialize raw LLM output into the specified DTO type.
    /// Tries JSON deserialization first; on failure, returns an error with
    /// code <see cref="ErrorCodes.LlmParseFailure"/>.
    /// </summary>
    /// <typeparam name="T">The target DTO type.</typeparam>
    /// <param name="rawResponse">The raw text response from the LLM.</param>
    /// <returns>
    /// A <see cref="Result{TValue, TError}"/> containing the deserialized DTO
    /// on success, or an <see cref="OuroborosError"/> on failure.
    /// </returns>
    public static Result<T, OuroborosError> TryParseJson<T>(string rawResponse)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return Result<T, OuroborosError>.Failure(
                OuroborosError.From(ErrorCodes.LlmParseFailure, "LLM response is empty or whitespace."));
        }

        var jsonBlock = ExtractJsonBlock(rawResponse);
        if (jsonBlock is null)
        {
            return Result<T, OuroborosError>.Failure(
                OuroborosError.From(ErrorCodes.LlmParseFailure,
                    "No JSON block found in LLM response."));
        }

        try
        {
            var dto = JsonSerializer.Deserialize<T>(jsonBlock, JsonOptions);
            if (dto is null)
            {
                return Result<T, OuroborosError>.Failure(
                    OuroborosError.From(ErrorCodes.LlmParseFailure,
                        "JSON deserialization returned null."));
            }

            return Result<T, OuroborosError>.Success(dto);
        }
        catch (JsonException ex)
        {
            return Result<T, OuroborosError>.Failure(new OuroborosError
            {
                Code = ErrorCodes.LlmParseFailure,
                Message = "JSON deserialization failed.",
                Detail = ex.Message,
                InnerException = ex,
            });
        }
    }

    /// <summary>
    /// Attempts structured JSON parsing first, falling back to the provided
    /// legacy string parser when JSON parsing fails.
    /// </summary>
    /// <typeparam name="T">The target DTO type.</typeparam>
    /// <param name="rawResponse">The raw text response from the LLM.</param>
    /// <param name="fallbackParser">
    /// A function that parses the raw text using legacy string-matching logic.
    /// This function should return <c>null</c> if it cannot parse the input.
    /// </param>
    /// <returns>
    /// A <see cref="Result{TValue, TError}"/> containing the deserialized DTO
    /// (from either JSON or fallback parsing) on success, or an
    /// <see cref="OuroborosError"/> if both paths fail.
    /// </returns>
    public static Result<T, OuroborosError> ParseWithFallback<T>(
        string rawResponse,
        Func<string, T?> fallbackParser)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(fallbackParser);

        // Try JSON first
        var jsonResult = TryParseJson<T>(rawResponse);
        if (jsonResult.IsSuccess)
            return jsonResult;

        // Fall back to legacy string parsing
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return Result<T, OuroborosError>.Failure(
                OuroborosError.From(ErrorCodes.LlmParseFailure,
                    "LLM response is empty or whitespace; fallback parser skipped."));
        }

        try
        {
            var fallbackResult = fallbackParser(rawResponse);
            if (fallbackResult is not null)
                return Result<T, OuroborosError>.Success(fallbackResult);

            return Result<T, OuroborosError>.Failure(
                OuroborosError.From(ErrorCodes.LlmParseFailure,
                    "Both JSON and fallback parsing failed to produce a result."));
        }
        catch (Exception ex)
        {
            return Result<T, OuroborosError>.Failure(new OuroborosError
            {
                Code = ErrorCodes.LlmParseFailure,
                Message = "Fallback parser threw an exception.",
                Detail = ex.Message,
                InnerException = ex,
            });
        }
    }

    /// <summary>
    /// Builds a JSON schema instruction suffix that can be appended to LLM prompts
    /// to request structured JSON output matching a given DTO type.
    /// </summary>
    /// <typeparam name="T">The DTO type to describe.</typeparam>
    /// <returns>A string containing the JSON format instruction.</returns>
    public static string BuildJsonSchemaInstruction<T>()
    {
        var schema = BuildSchemaDescription(typeof(T));
        return $"""

            Respond ONLY with valid JSON matching this schema (no markdown fences, no extra text):
            {schema}
            """;
    }

    /// <summary>
    /// Extracts a JSON block from raw LLM text. Handles:
    /// <list type="bullet">
    ///   <item>Markdown fenced code blocks (<c>```json ... ```</c>)</item>
    ///   <item>Bare JSON objects (<c>{{ ... }}</c>)</item>
    ///   <item>Bare JSON arrays (<c>[ ... ]</c>)</item>
    /// </list>
    /// </summary>
    /// <param name="rawResponse">The raw LLM output.</param>
    /// <returns>The extracted JSON string, or <c>null</c> if no JSON was found.</returns>
    internal static string? ExtractJsonBlock(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return null;

        // Try markdown fenced code block first: ```json ... ``` or ``` ... ```
        var fenceMatch = MarkdownJsonFenceRegex().Match(rawResponse);
        if (fenceMatch.Success)
            return fenceMatch.Groups[1].Value.Trim();

        // Try bare JSON object
        var objectMatch = BareJsonObjectRegex().Match(rawResponse);
        if (objectMatch.Success)
            return objectMatch.Value;

        // Try bare JSON array
        var arrayMatch = BareJsonArrayRegex().Match(rawResponse);
        if (arrayMatch.Success)
            return arrayMatch.Value;

        return null;
    }

    private static string BuildSchemaDescription(Type type)
    {
        if (type == typeof(SubgoalResponse))
        {
            return """
                {
                  "subgoals": [
                    { "description": "string", "type": "Primary|Secondary|Instrumental|Safety", "priority": 0.0 }
                  ]
                }
                """;
        }

        if (type == typeof(ConsistencyAnalysisDto))
        {
            return """
                {
                  "isConsistent": true,
                  "conflicts": ["string"],
                  "missingPrerequisites": ["string"]
                }
                """;
        }

        if (type == typeof(GroundingResponseDto))
        {
            return """
                {
                  "mettaType": "string",
                  "properties": ["string"],
                  "relations": ["string"]
                }
                """;
        }

        if (type == typeof(AlignmentResponseDto))
        {
            return """
                {
                  "isAligned": true,
                  "explanation": "string"
                }
                """;
        }

        if (type == typeof(RuleExtractionResponseDto))
        {
            return """
                {
                  "rules": [
                    {
                      "name": "string",
                      "metta": "string",
                      "description": "string",
                      "preconditions": ["string"],
                      "effects": ["string"]
                    }
                  ]
                }
                """;
        }

        if (type == typeof(MeTTaConversionResponseDto))
        {
            return """
                {
                  "expression": "string"
                }
                """;
        }

        // Generic fallback: instruct the LLM to produce a JSON object
        return "{ /* JSON object matching the expected response schema */ }";
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?([\s\S]*?)\n?\s*```", RegexOptions.Singleline)]
    private static partial Regex MarkdownJsonFenceRegex();

    [GeneratedRegex(@"\{[\s\S]*\}", RegexOptions.Singleline)]
    private static partial Regex BareJsonObjectRegex();

    [GeneratedRegex(@"\[[\s\S]*\]", RegexOptions.Singleline)]
    private static partial Regex BareJsonArrayRegex();
}
