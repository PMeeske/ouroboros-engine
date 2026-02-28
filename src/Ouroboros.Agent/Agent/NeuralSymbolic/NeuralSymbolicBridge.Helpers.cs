#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Neural-Symbolic Bridge - Private Helper Methods
// Parsing, prompt building, and reasoning helpers
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

using System.Text.RegularExpressions;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Private helper methods for NeuralSymbolicBridge.
/// </summary>
public sealed partial class NeuralSymbolicBridge
{
    private string BuildRuleExtractionPrompt(Skill skill)
    {
        var stepsText = string.Join("\n", skill.Steps.Select((s, i) => $"{i + 1}. {s}"));

        return $@"Extract symbolic rules from this learned skill:

Skill: {skill.Name}
Description: {skill.Description}
Steps:
{stepsText}

For each rule, provide:
1. Rule name
2. MeTTa representation (use S-expression syntax)
3. Natural language description
4. Preconditions
5. Effects

Format each rule as:
RULE: <name>
METTA: <metta-expression>
DESCRIPTION: <description>
PRECONDITIONS: <condition1>, <condition2>, ...
EFFECTS: <effect1>, <effect2>, ...
---";
    }

    private List<SymbolicRule> ParseExtractedRules(string response, Skill skill)
    {
        var rules = new List<SymbolicRule>();
        var ruleBlocks = response.Split("---", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in ruleBlocks)
        {
            try
            {
                var name = ExtractField(block, "RULE:");
                var metta = ExtractField(block, "METTA:");
                var description = ExtractField(block, "DESCRIPTION:");
                var preconditions = ExtractList(block, "PRECONDITIONS:");
                var effects = ExtractList(block, "EFFECTS:");

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(metta))
                {
                    rules.Add(new SymbolicRule(
                        name,
                        metta,
                        description,
                        preconditions,
                        effects,
                        skill.SuccessRate,
                        RuleSource.ExtractedFromSkill));
                }
            }
            catch
            {
                // Skip malformed rule blocks
                continue;
            }
        }

        return rules;
    }

    private MeTTaExpression ParseMeTTaExpression(string response)
    {
        var cleaned = response.Trim();

        // Extract S-expression
        var match = SExpressionRegex().Match(cleaned);
        var rawExpression = match.Success ? match.Value : cleaned;

        // Parse symbols and variables
        var symbols = new List<string>();
        var variables = new List<string>();
        var tokens = TokenRegex().Matches(rawExpression);

        foreach (Match token in tokens)
        {
            if (token.Value.StartsWith("$"))
                variables.Add(token.Value);
            else if (!string.IsNullOrWhiteSpace(token.Value))
                symbols.Add(token.Value);
        }

        var type = DetermineExpressionType(rawExpression);

        return new MeTTaExpression(
            rawExpression,
            type,
            symbols,
            variables,
            new Dictionary<string, object>());
    }

    private ExpressionType DetermineExpressionType(string expression)
    {
        if (expression.Contains("$"))
            return ExpressionType.Variable;
        if (expression.Contains("->") || expression.Contains("=>"))
            return ExpressionType.Rule;
        if (expression.Contains("?"))
            return ExpressionType.Query;
        if (expression.StartsWith("(") && expression.Contains(" "))
            return ExpressionType.Expression;

        return ExpressionType.Atom;
    }

    private async Task<Result<(string answer, List<ReasoningStep> steps, double confidence), string>> TrySymbolicReasoning(
        string query, CancellationToken ct)
    {
        try
        {
            var result = await _knowledgeBase.ExecuteMeTTaQueryAsync(query, ct);
            if (result.IsFailure)
                return Result<(string, List<ReasoningStep>, double), string>.Failure(result.Error);

            var steps = new List<ReasoningStep>
            {
                new ReasoningStep(1, "Symbolic query execution", query, ReasoningStepType.SymbolicDeduction)
            };

            return Result<(string, List<ReasoningStep>, double), string>.Success((result.Value, steps, 0.85));
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<(string, List<ReasoningStep>, double), string>.Failure(ex.Message);
        }
    }

    private async Task<Result<(string answer, List<ReasoningStep> steps, double confidence), string>> TryNeuralReasoning(
        string query, CancellationToken ct)
    {
        try
        {
            var prompt = $"Answer the following question using your knowledge:\n\n{query}\n\nAnswer:";
            var response = await _llm.GenerateTextAsync(prompt, ct);

            var steps = new List<ReasoningStep>
            {
                new ReasoningStep(1, "Neural inference", "LLM reasoning", ReasoningStepType.NeuralInference)
            };

            return Result<(string, List<ReasoningStep>, double), string>.Success((response, steps, 0.7));
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<(string, List<ReasoningStep>, double), string>.Failure(ex.Message);
        }
    }

    private string BuildConsistencyCheckPrompt(MetaAI.Hypothesis hypothesis, IReadOnlyList<SymbolicRule> knowledgeBase)
    {
        var rulesText = string.Join("\n", knowledgeBase.Take(10).Select(r =>
            $"- {r.Name}: {r.NaturalLanguageDescription}"));

        return $@"Check if this hypothesis is logically consistent with the knowledge base:

Hypothesis: {hypothesis.Statement}
Domain: {hypothesis.Domain}

Knowledge Base Rules:
{rulesText}

Analyze:
1. Are there any logical conflicts?
2. Are there missing prerequisites?
3. Is the hypothesis consistent?

Respond with:
CONSISTENT: Yes/No
CONFLICTS: <conflict descriptions, if any>
MISSING: <missing prerequisites, if any>";
    }

    private (bool isConsistent, List<LogicalConflict> conflicts, List<string> missing) ParseConsistencyAnalysis(
        string response, IReadOnlyList<SymbolicRule> knowledgeBase)
    {
        var isConsistent = response.Contains("CONSISTENT: Yes", StringComparison.OrdinalIgnoreCase);
        var conflicts = new List<LogicalConflict>();
        var missing = new List<string>();

        // Extract conflicts
        var conflictsMatch = ConflictsRegex().Match(response);
        if (conflictsMatch.Success)
        {
            var conflictText = conflictsMatch.Groups[1].Value.Trim();
            if (!conflictText.Contains("None", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(conflictText))
            {
                // Create a conflict with null rules since the LLM analysis doesn't identify specific conflicting rules
                // A more sophisticated implementation could use additional LLM calls to identify the specific rules
                conflicts.Add(new LogicalConflict(
                    conflictText,
                    null, // Rule1 is unknown from LLM analysis
                    null, // Rule2 is unknown from LLM analysis
                    "Review the conflicting rules and resolve inconsistencies manually. Consider refining the hypothesis or updating the knowledge base."));
            }
        }

        // Extract missing prerequisites
        var missingMatch = MissingRegex().Match(response);
        if (missingMatch.Success)
        {
            var missingText = missingMatch.Groups[1].Value.Trim();
            if (!missingText.Contains("None", StringComparison.OrdinalIgnoreCase))
            {
                missing.AddRange(missingText.Split(',').Select(m => m.Trim()));
            }
        }

        return (isConsistent, conflicts, missing);
    }

    private (string type, List<string> properties, List<string> relations) ParseGroundingResponse(string response)
    {
        var type = ExtractField(response, "Type:") ?? "Concept";
        var properties = ExtractList(response, "Properties:");
        var relations = ExtractList(response, "Relations:");

        return (type, properties, relations);
    }

    private string ExtractField(string text, string fieldName)
    {
        var match = Regex.Match(text, $@"{Regex.Escape(fieldName)}\s*(.+?)(?=\n[A-Z]+:|$)", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private List<string> ExtractList(string text, string fieldName)
    {
        var field = ExtractField(text, fieldName);
        if (string.IsNullOrWhiteSpace(field))
            return new List<string>();

        return field.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    [GeneratedRegex(@"\([^)]+\)")]
    private static partial Regex SExpressionRegex();

    [GeneratedRegex(@"\$\w+|\w+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"CONFLICTS:\s*(.+?)(?=MISSING:|$)", RegexOptions.Singleline)]
    private static partial Regex ConflictsRegex();

    [GeneratedRegex(@"MISSING:\s*(.+?)$", RegexOptions.Singleline)]
    private static partial Regex MissingRegex();
}
