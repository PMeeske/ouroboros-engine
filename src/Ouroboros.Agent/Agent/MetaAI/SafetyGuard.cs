#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Safety Guard Implementation
// Permission-based safe execution
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of safety guard for permission-based execution.
/// </summary>
public sealed class SafetyGuard : ISafetyGuard
{
    private const double RiskThresholdForDenial = 0.8;

    private readonly ConcurrentDictionary<string, PermissionPolicy> _permissionPolicies = new();
    private readonly ConcurrentDictionary<string, PermissionLevel> _agentPermissions = new();
    private readonly PermissionLevel _defaultLevel;

    public SafetyGuard(PermissionLevel defaultLevel = PermissionLevel.Read)
    {
        _defaultLevel = defaultLevel;
        InitializeDefaultPermissions();
    }

    /// <summary>
    /// Checks if an action is safe to execute.
    /// </summary>
    public async Task<SafetyCheckResult> CheckActionSafetyAsync(
        string actionName,
        IReadOnlyDictionary<string, object> parameters,
        object? context = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actionName);
        ArgumentNullException.ThrowIfNull(parameters);

        PermissionLevel requiredLevel = GetRequiredPermissionLevel(actionName);
        string permissionReason = _permissionPolicies.TryGetValue(actionName, out var policy)
            ? policy.Description
            : $"Permission for {actionName} action";
        List<Permission> requiredPermissions = new() { new Permission(actionName, requiredLevel, permissionReason) };
        List<string> violations = new();

        // Check for dangerous patterns
        if (ContainsDangerousPatterns(actionName, parameters))
        {
            violations.Add("Action contains potentially dangerous patterns");
        }

        // Check parameter safety
        foreach (KeyValuePair<string, object> param in parameters)
        {
            if (param.Value is string strValue && ContainsInjectionPatterns(strValue))
            {
                violations.Add($"Parameter '{param.Key}' contains potential injection patterns");
            }
        }

        double riskScore = await AssessRiskAsync(actionName, parameters, ct);
        bool isAllowed = violations.Count == 0 && riskScore < RiskThresholdForDenial;
        string reason = isAllowed ? "Action is safe to execute" : string.Join("; ", violations);

        return new SafetyCheckResult(isAllowed, reason, requiredPermissions, riskScore, violations);
    }

    /// <summary>
    /// Checks safety of an action (simplified overload).
    /// </summary>
    public async Task<SafetyCheckResult> CheckSafetyAsync(
        string action,
        PermissionLevel permissionLevel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var parameters = new Dictionary<string, object>
        {
            ["requiredLevel"] = permissionLevel
        };

        return await CheckActionSafetyAsync(action, parameters, null, ct);
    }

    /// <summary>
    /// Sandboxes a step for safe execution.
    /// </summary>
    public Task<SandboxResult> SandboxStepAsync(
        PlanStep step,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(step);

        try
        {
            // Create sandboxed version with restricted parameters
            Dictionary<string, object> sandboxedParams = new Dictionary<string, object>();
            List<string> restrictions = new List<string>();

            foreach (KeyValuePair<string, object> param in step.Parameters)
            {
                if (param.Value is string strValue)
                {
                    // Sanitize string values
                    string sanitized = SanitizeString(strValue);
                    sandboxedParams[param.Key] = sanitized;
                    if (sanitized != strValue)
                    {
                        restrictions.Add($"Parameter '{param.Key}' was sanitized");
                    }
                }
                else
                {
                    sandboxedParams[param.Key] = param.Value;
                }
            }

            // Add sandbox metadata
            sandboxedParams["__sandboxed__"] = true;
            sandboxedParams["__original_action__"] = step.Action;
            restrictions.Add("Execution is sandboxed");

            PlanStep sandboxedStep = step with { Parameters = sandboxedParams };

            return Task.FromResult(new SandboxResult(true, sandboxedStep, restrictions, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SandboxResult(false, null, Array.Empty<string>(), ex.Message));
        }
    }

    /// <summary>
    /// Checks if an agent has the required permissions.
    /// </summary>
    public Task<bool> CheckPermissionsAsync(
        string agentId,
        IReadOnlyList<Permission> permissions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(permissions);

        // Get agent's permission level (or use default)
        PermissionLevel agentLevel = _agentPermissions.GetValueOrDefault(agentId, _defaultLevel);

        // Check if agent has sufficient permission for all required permissions
        foreach (Permission permission in permissions)
        {
            if (agentLevel < permission.Level)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Assesses the risk level of an action.
    /// </summary>
    public Task<double> AssessRiskAsync(
        string actionName,
        IReadOnlyDictionary<string, object> parameters,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actionName);
        ArgumentNullException.ThrowIfNull(parameters);

        double risk = 0.0;

        // Base risk from action name patterns
        string actionLower = actionName.ToLowerInvariant();
        if (actionLower.Contains("delete") || actionLower.Contains("drop") || actionLower.Contains("remove"))
            risk += 0.4;
        else if (actionLower.Contains("system") || actionLower.Contains("admin") || actionLower.Contains("exec"))
            risk += 0.5;
        else if (actionLower.Contains("write") || actionLower.Contains("update") || actionLower.Contains("create"))
            risk += 0.2;
        else if (actionLower.Contains("read") || actionLower.Contains("get") || actionLower.Contains("list"))
            risk += 0.1;

        // Additional risk from dangerous patterns
        if (ContainsDangerousPatterns(actionName, parameters))
            risk += 0.3;

        // Risk from parameter injection patterns
        foreach (KeyValuePair<string, object> param in parameters)
        {
            if (param.Value is string strValue && ContainsInjectionPatterns(strValue))
            {
                risk += 0.2;
            }
        }

        // Cap at 1.0
        return Task.FromResult(Math.Min(risk, 1.0));
    }

    /// <summary>
    /// Sets permission level for an agent.
    /// </summary>
    public void SetAgentPermissionLevel(string agentId, PermissionLevel level)
    {
        _agentPermissions[agentId] = level;
    }

    /// <summary>
    /// Registers a permission policy for an action.
    /// </summary>
    public void RegisterPermissionPolicy(string actionName, PermissionLevel level, string description)
    {
        _permissionPolicies[actionName] = new PermissionPolicy(actionName, level, description);
    }

    // Backward compatibility methods for existing callers
    // These adapt the old API to the new Foundation interface

    /// <summary>
    /// Legacy method: Checks if an operation is safe to execute (synchronous).
    /// Kept for backward compatibility - prefer CheckActionSafetyAsync.
    /// WARNING: This method blocks on async code and should ONLY be used in console applications
    /// or background tasks. Do NOT use in ASP.NET Core, UI contexts, or any environment with
    /// a synchronization context, as it can still cause deadlocks despite Task.Run.
    /// </summary>
    [Obsolete("Use CheckActionSafetyAsync instead")]
    public SafetyCheckResult CheckSafety(
        string operation,
        Dictionary<string, object> parameters,
        PermissionLevel currentLevel)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(parameters);

        // Task.Run helps but doesn't eliminate deadlock risk in all contexts
        IReadOnlyDictionary<string, object> readOnlyParams = parameters;
        SafetyCheckResult result = Task.Run(async () =>
            await CheckActionSafetyAsync(operation, readOnlyParams, null, CancellationToken.None))
            .GetAwaiter()
            .GetResult();

        return result;
    }

    /// <summary>
    /// Legacy method: Sandboxes a plan step for safe execution.
    /// Kept for backward compatibility.
    /// </summary>
    [Obsolete("This method is deprecated and will be removed in a future version")]
    public PlanStep SandboxStep(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        // Create sandboxed version with restricted parameters
        Dictionary<string, object> sandboxedParams = new Dictionary<string, object>();

        foreach (KeyValuePair<string, object> param in step.Parameters)
        {
            if (param.Value is string strValue)
            {
                // Sanitize string values
                sandboxedParams[param.Key] = SanitizeString(strValue);
            }
            else
            {
                sandboxedParams[param.Key] = param.Value;
            }
        }

        // Add sandbox metadata
        sandboxedParams["__sandboxed__"] = true;
        sandboxedParams["__original_action__"] = step.Action;

        return step with { Parameters = sandboxedParams };
    }

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

    // Internal helper record for permission policies
    private sealed record PermissionPolicy(string ActionName, PermissionLevel Level, string Description);
}