#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Concurrent;
using Ouroboros.Abstractions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Partial class containing sanitization, pattern detection, permission helpers,
/// and MeTTa symbolic reasoning for safety checks.
/// </summary>
public sealed partial class SafetyGuard
{
    private string SanitizeString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        // Remove potentially dangerous characters
        string sanitized = value
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&#39;")
            .Replace("\"", "&quot;");

        // Limit length to prevent DOS
        if (sanitized.Length > 10000)
        {
            sanitized = sanitized.Substring(0, 10000);
        }

        return sanitized;
    }

    private PermissionLevel GetRequiredPermissionLevel(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return _defaultLevel;

        // Check registered policies
        if (_permissionPolicies.TryGetValue(action, out PermissionPolicy? policy))
        {
            return policy.Level;
        }

        // Determine based on action patterns
        string actionLower = action.ToLowerInvariant();

        if (actionLower.Contains("read") || actionLower.Contains("get") || actionLower.Contains("list"))
            return PermissionLevel.Read;

        if (actionLower.Contains("delete") || actionLower.Contains("drop") || actionLower.Contains("remove"))
            return PermissionLevel.Admin;

        if (actionLower.Contains("system") || actionLower.Contains("admin"))
            return PermissionLevel.Admin;

        if (actionLower.Contains("write") || actionLower.Contains("update") || actionLower.Contains("create"))
            return PermissionLevel.Write;

        if (actionLower.Contains("exec") || actionLower.Contains("run") || actionLower.Contains("execute"))
            return PermissionLevel.Execute;

        return _defaultLevel;
    }

    private void InitializeDefaultPermissions()
    {
        // Register common tool permissions with new permission levels
        RegisterPermissionPolicy("math", PermissionLevel.Read, "Mathematical calculations");
        RegisterPermissionPolicy("search", PermissionLevel.Read, "Search operations");
        RegisterPermissionPolicy("llm", PermissionLevel.Execute, "LLM text generation");
        RegisterPermissionPolicy("run_usedraft", PermissionLevel.Execute, "Generate draft");
        RegisterPermissionPolicy("run_usecritique", PermissionLevel.Execute, "Critique content");
        RegisterPermissionPolicy("file_write", PermissionLevel.Write, "File write operations");
        RegisterPermissionPolicy("qdrant_admin", PermissionLevel.Write, "Qdrant vector database administration");
    }

    private bool ContainsDangerousPatterns(string operation, IReadOnlyDictionary<string, object> parameters)
    {
        string[] dangerousPatterns = new[]
        {
            "eval", "exec", "system", "shell", "subprocess",
            "rm -rf", "delete *", "drop table", "truncate"
        };

        string combined = operation + " " + string.Join(" ", parameters.Values.Select(v => v?.ToString() ?? ""));
        string lowerCombined = combined.ToLowerInvariant();

        return dangerousPatterns.Any(pattern => lowerCombined.Contains(pattern));
    }

    private bool ContainsInjectionPatterns(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] injectionPatterns = new[]
        {
            "';", "\"; ", "' OR '1'='1", "\" OR \"1\"=\"1",
            "../", "..\\", "<script", "javascript:",
            "onload=", "onerror="
        };

        return injectionPatterns.Any(pattern =>
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Queries MeTTa symbolic reasoning for action safety.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Form representing MeTTa's safety assessment (Mark/Void/Imaginary).</returns>
    private async Task<Form> QueryMeTTaSafetyAsync(string action, CancellationToken ct)
    {
        if (_mettaEngine == null)
        {
            return Form.Imaginary; // No engine means uncertain
        }

        try
        {
            // Escape the action string for MeTTa query
            string escapedAction = EscapeMeTTaString(action);

            // Query: (IsSafeAction "action")
            string query = $"(IsSafeAction \"{escapedAction}\")";
            Result<string, string> result = await _mettaEngine.ExecuteQueryAsync(query, ct);

            // Map result to Form
            return result.Match(
                onSuccess: queryResult =>
                {
                    string trimmed = queryResult.Trim();

                    // MeTTa returns atoms like Mark, Void, or empty results
                    if (trimmed.Contains("Mark", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Contains("\u2310", StringComparison.OrdinalIgnoreCase))
                    {
                        return Form.Mark;
                    }
                    else if (trimmed.Contains("Void", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.Contains("\u2205", StringComparison.OrdinalIgnoreCase) ||
                             string.IsNullOrWhiteSpace(trimmed))
                    {
                        return Form.Void;
                    }
                    else
                    {
                        // Unknown result -> uncertain
                        return Form.Imaginary;
                    }
                },
                onFailure: _ => Form.Imaginary); // Query failure -> uncertain
        }
        catch
        {
            // Exception during query -> uncertain
            return Form.Imaginary;
        }
    }

    /// <summary>
    /// Adds MeTTa safety rules to the knowledge base.
    /// Should be called during orchestrator initialization.
    /// </summary>
    /// <param name="instanceId">The Ouroboros instance ID for scoping rules.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> AddMeTTaSafetyRulesAsync(string instanceId, CancellationToken ct = default)
    {
        if (_mettaEngine == null)
        {
            return Result<Unit, string>.Success(Unit.Value); // No engine, skip silently
        }

        try
        {
            // Escape the instance ID to prevent injection attacks
            string escapedInstanceId = EscapeMeTTaString(instanceId);

            // Rule: Actions are safe if the instance respects safety constraints
            // and the action doesn't match destructive patterns
            string safetyRule = $@"
(= (IsSafeAction $action)
   (if (and (Respects (OuroborosInstance ""{escapedInstanceId}"") NoSelfDestruction)
            (not (MatchesPattern $action ""destructive"")))
       Mark
       Void))

(= (MatchesPattern $action ""destructive"")
   (or (contains $action ""delete self"")
       (contains $action ""terminate"")
       (contains $action ""disable oversight"")))";

            Result<string, string> result = await _mettaEngine.ApplyRuleAsync(safetyRule, ct);

            return result.Match(
                onSuccess: _ => Result<Unit, string>.Success(Unit.Value),
                onFailure: error => Result<Unit, string>.Failure($"Failed to add MeTTa safety rules: {error}"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Exception adding MeTTa safety rules: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes a string for use in MeTTa queries.
    /// </summary>
    private static string EscapeMeTTaString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Escape quotes and backslashes for MeTTa
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}
