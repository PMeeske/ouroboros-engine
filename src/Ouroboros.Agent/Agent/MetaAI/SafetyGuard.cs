// ==========================================================
// Safety Guard Implementation
// Permission-based safe execution
// ==========================================================

using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of safety guard for permission-based execution.
/// Combines string-matching safety checks with optional MeTTa symbolic reasoning
/// for neuro-symbolic safety validation.
/// </summary>
public sealed partial class SafetyGuard : ISafetyGuard
{
    private const double RiskThresholdForDenial = 0.8;

    private readonly ConcurrentDictionary<string, PermissionPolicy> _permissionPolicies = new();
    private readonly ConcurrentDictionary<string, PermissionLevel> _agentPermissions = new();
    private readonly PermissionLevel _defaultLevel;
    private readonly IMeTTaEngine? _mettaEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafetyGuard"/> class.
    /// </summary>
    /// <param name="defaultLevel">The default permission level.</param>
    /// <param name="mettaEngine">Optional MeTTa engine for symbolic safety reasoning.</param>
    public SafetyGuard(PermissionLevel defaultLevel = PermissionLevel.Read, IMeTTaEngine? mettaEngine = null)
    {
        _defaultLevel = defaultLevel;
        _mettaEngine = mettaEngine;
        InitializeDefaultPermissions();
    }

    /// <summary>
    /// Checks if an action is safe to execute using neuro-symbolic safety validation.
    /// First checks with OuroborosAtom string matching (if available in context),
    /// then queries MeTTa symbolic reasoning (if engine is available).
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

        // Step 1: Check with OuroborosAtom.IsSafeAction() if available in context
        if (context is OuroborosAtom atom && !atom.IsSafeAction(actionName))
        {
            violations.Add("Action rejected by OuroborosAtom safety constraints");
            return SafetyCheckResult.Denied(
                "Action violates Ouroboros safety constraints",
                violations,
                1.0);
        }

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

        double riskScore = await AssessRiskAsync(actionName, parameters, ct).ConfigureAwait(false);

        // Step 2: Query MeTTa symbolic reasoning if engine is available
        if (_mettaEngine != null)
        {
            Form mettaResult = await QueryMeTTaSafetyAsync(actionName, ct).ConfigureAwait(false);

            // Map Form results to safety decisions
            return mettaResult.Match(
                onMark: () =>
                {
                    // Mark (certain affirmative) → safe
                    bool isAllowed = violations.Count == 0 && riskScore < RiskThresholdForDenial;
                    string reason = isAllowed
                        ? "Action approved by symbolic reasoning"
                        : string.Join("; ", violations);
                    return new SafetyCheckResult(isAllowed, reason, requiredPermissions, riskScore, violations);
                },
                onVoid: () =>
                {
                    // Void (certain negative) → unsafe
                    violations.Add("Action rejected by MeTTa symbolic reasoning");
                    return SafetyCheckResult.Denied(
                        "Action violates symbolic safety rules",
                        violations,
                        Math.Max(riskScore, 0.9));
                },
                onImaginary: () =>
                {
                    // Imaginary (uncertain) → requires review
                    violations.Add("Symbolic reasoning is uncertain about action safety");
                    return new SafetyCheckResult(
                        false, // Not allowed without review
                        "Action requires human review (symbolic uncertainty)",
                        requiredPermissions,
                        Math.Max(riskScore, 0.6),
                        violations);
                });
        }

        // Step 3: Fallback to atom-only check if MeTTa is unavailable
        bool isAllowedFallback = violations.Count == 0 && riskScore < RiskThresholdForDenial;
        string reasonFallback = isAllowedFallback ? "Action is safe to execute" : string.Join("; ", violations);

        return new SafetyCheckResult(isAllowedFallback, reasonFallback, requiredPermissions, riskScore, violations);
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

        return await CheckActionSafetyAsync(action, parameters, null, ct).ConfigureAwait(false);
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
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

    /// <summary>
    /// Checks if an operation is safe to execute (synchronous).
    /// Uses a pure sync code path to avoid sync-over-async deadlocks.
    /// Prefer CheckActionSafetyAsync for new code.
    /// </summary>
    public SafetyCheckResult CheckSafety(
        string operation,
        Dictionary<string, object> parameters,
        PermissionLevel currentLevel)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(parameters);

        IReadOnlyDictionary<string, object> readOnlyParams = parameters;

        PermissionLevel requiredLevel = GetRequiredPermissionLevel(operation);
        string permissionReason = _permissionPolicies.TryGetValue(operation, out var policy)
            ? policy.Description
            : $"Permission for {operation} action";
        List<Permission> requiredPermissions = new() { new Permission(operation, requiredLevel, permissionReason) };
        List<string> violations = new();

        // Check for dangerous patterns
        if (ContainsDangerousPatterns(operation, readOnlyParams))
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

        // Inline sync risk assessment (mirrors AssessRiskAsync logic)
        double risk = 0.0;
        string actionLower = operation.ToLowerInvariant();
        if (actionLower.Contains("delete") || actionLower.Contains("drop") || actionLower.Contains("remove"))
            risk += 0.4;
        else if (actionLower.Contains("system") || actionLower.Contains("admin") || actionLower.Contains("exec"))
            risk += 0.5;
        else if (actionLower.Contains("write") || actionLower.Contains("update") || actionLower.Contains("create"))
            risk += 0.2;
        else if (actionLower.Contains("read") || actionLower.Contains("get") || actionLower.Contains("list"))
            risk += 0.1;

        if (ContainsDangerousPatterns(operation, readOnlyParams))
            risk += 0.3;

        foreach (KeyValuePair<string, object> param in parameters)
        {
            if (param.Value is string strValue && ContainsInjectionPatterns(strValue))
                risk += 0.2;
        }

        risk = Math.Min(risk, 1.0);

        // Sync path skips MeTTa reasoning (async-only) — uses string-matching fallback
        bool isAllowed = violations.Count == 0 && risk < RiskThresholdForDenial;
        string reason = isAllowed ? "Action is safe to execute" : string.Join("; ", violations);

        return new SafetyCheckResult(isAllowed, reason, requiredPermissions, risk, violations);
    }

    /// <summary>
    /// Sandboxes a plan step for safe execution.
    /// Prefer SandboxStepAsync for new code.
    /// </summary>
    public PlanStep SandboxStep(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        Dictionary<string, object> sandboxedParams = new Dictionary<string, object>();

        foreach (KeyValuePair<string, object> param in step.Parameters)
        {
            if (param.Value is string strValue)
            {
                sandboxedParams[param.Key] = SanitizeString(strValue);
            }
            else
            {
                sandboxedParams[param.Key] = param.Value;
            }
        }

        sandboxedParams["__sandboxed__"] = true;
        sandboxedParams["__original_action__"] = step.Action;

        return step with { Parameters = sandboxedParams };
    }

    // Internal helper record for permission policies
    private sealed record PermissionPolicy(string ActionName, PermissionLevel Level, string Description);
}