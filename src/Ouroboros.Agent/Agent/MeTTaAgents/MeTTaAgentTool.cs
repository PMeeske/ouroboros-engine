// <copyright file="MeTTaAgentTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.Json;
using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Tool that allows the orchestrating LLM to define, spawn, and task sub-agents
/// by writing MeTTa atoms. The LLM speaks MeTTa to control agents.
/// </summary>
public sealed partial class MeTTaAgentTool : ITool
{
    private readonly MeTTaAgentRuntime _runtime;
    private readonly IMeTTaEngine _engine;
    private int _taskCounter;

    /// <summary>
    /// Creates a new MeTTa agent management tool.
    /// </summary>
    /// <param name="runtime">The agent runtime to manage.</param>
    /// <param name="engine">The MeTTa engine for symbolic operations.</param>
    public MeTTaAgentTool(MeTTaAgentRuntime runtime, IMeTTaEngine engine)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc/>
    public string Name => "metta_agents";

    /// <inheritdoc/>
    public string Description =>
        "Manage MeTTa-native sub-agents. Operations: " +
        "define (create agent blueprint), spawn (materialize agent), " +
        "task (send task to agent), route (route by capability), " +
        "pipeline (multi-agent chain), status (get statuses), " +
        "terminate (shut down agent), list (list all agents).";

    /// <inheritdoc/>
    public string? JsonSchema => """
{
  "type": "object",
  "properties": {
    "operation": {
      "type": "string",
      "enum": ["define", "spawn", "task", "route", "pipeline", "status", "terminate", "list"]
    },
    "agent_id": { "type": "string" },
    "provider": { "type": "string", "enum": ["Ollama", "OllamaCloud", "OpenAI", "LocalMock"] },
    "model": { "type": "string" },
    "role": { "type": "string" },
    "system_prompt": { "type": "string" },
    "capability": { "type": "string" },
    "capabilities": { "type": "array", "items": { "type": "string" } },
    "task_id": { "type": "string" },
    "prompt": { "type": "string" },
    "agent_ids": { "type": "array", "items": { "type": "string" } },
    "max_tokens": { "type": "integer", "default": 4096 },
    "temperature": { "type": "number", "default": 0.5 }
  },
  "required": ["operation"]
}
""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            string? operation = root.TryGetProperty("operation", out var opElem)
                ? opElem.GetString() : null;

            if (string.IsNullOrEmpty(operation))
                return Result<string, string>.Failure("Missing 'operation' field");

            return operation switch
            {
                "define" => await HandleDefineAsync(root, ct).ConfigureAwait(false),
                "spawn" => await HandleSpawnAsync(root, ct).ConfigureAwait(false),
                "task" => await HandleTaskAsync(root, ct).ConfigureAwait(false),
                "route" => await HandleRouteAsync(root, ct).ConfigureAwait(false),
                "pipeline" => await HandlePipelineAsync(root, ct).ConfigureAwait(false),
                "status" => HandleStatus(),
                "terminate" => await HandleTerminateAsync(root, ct).ConfigureAwait(false),
                "list" => HandleList(),
                _ => Result<string, string>.Failure($"Unknown operation: {operation}")
            };
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Invalid JSON input: {ex.Message}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<string, string>.Failure($"Agent tool error: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> HandleDefineAsync(JsonElement root, CancellationToken ct)
    {
        string? agentId = GetOptionalString(root, "agent_id");
        string? provider = GetOptionalString(root, "provider");
        string? model = GetOptionalString(root, "model");
        string? role = GetOptionalString(root, "role");
        string? systemPrompt = GetOptionalString(root, "system_prompt");

        if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(provider) ||
            string.IsNullOrEmpty(model) || string.IsNullOrEmpty(role))
        {
            return Result<string, string>.Failure(
                "Define requires: agent_id, provider, model, role");
        }

        int maxTokens = root.TryGetProperty("max_tokens", out var mt) ? mt.GetInt32() : 4096;
        float temperature = root.TryGetProperty("temperature", out var temp)
            ? (float)temp.GetDouble() : 0.5f;

        List<string>? capabilities = null;
        if (root.TryGetProperty("capabilities", out var capsElem))
        {
            capabilities = capsElem.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => s != null)
                .Cast<string>()
                .ToList();
        }

        var def = new MeTTaAgentDef(
            agentId, provider, model, role,
            systemPrompt ?? PromptTemplateLoader.GetPromptText("MeTTa", "DefaultAgentRole").Replace("{{$role}}", role.ToLowerInvariant()),
            maxTokens, temperature,
            Capabilities: capabilities);

        return await _runtime.DefineAgentAsync(def, autoSpawn: false, ct).ConfigureAwait(false);
    }

    private async Task<Result<string, string>> HandleSpawnAsync(JsonElement root, CancellationToken ct)
    {
        string? agentId = GetOptionalString(root, "agent_id");

        if (string.IsNullOrEmpty(agentId))
        {
            // Spawn all defined agents
            var result = await _runtime.SpawnAllAsync(ct).ConfigureAwait(false);
            return result.IsSuccess
                ? Result<string, string>.Success($"Spawned {result.Value} agents")
                : Result<string, string>.Failure(result.Error);
        }

        // Try to get existing AgentDef from MeTTa first
        string? provider = GetOptionalString(root, "provider");
        string? model = GetOptionalString(root, "model");

        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model))
        {
            // Query MeTTa for the existing definition (escape agentId to prevent injection)
            string escapedAgentId = MeTTaParsingHelpers.EscapeMeTTaString(agentId);
            var queryResult = await _engine.ExecuteQueryAsync(
                $"!(match &self (AgentDef \"{escapedAgentId}\" $prov $model $role $prompt $tokens $temp) " +
                $"(AgentDef \"{escapedAgentId}\" $prov $model $role $prompt $tokens $temp))", ct).ConfigureAwait(false);

            if (queryResult.IsFailure || string.IsNullOrWhiteSpace(queryResult.Value))
            {
                return Result<string, string>.Failure(
                    $"Agent '{agentId}' not defined in MeTTa. Provide provider and model to spawn ad-hoc.");
            }

            // Parse the AgentDef from MeTTa response and spawn it
            var defs = ParseAgentDefsFromMeTTa(queryResult.Value);
            if (defs.Count == 0)
            {
                return Result<string, string>.Failure(
                    $"Failed to parse AgentDef for '{agentId}' from MeTTa.");
            }

            var spawnResult = await _runtime.SpawnAgentAsync(defs[0], ct).ConfigureAwait(false);
            return spawnResult.IsSuccess
                ? Result<string, string>.Success($"Agent '{agentId}' spawned from definition")
                : Result<string, string>.Failure(spawnResult.Error);
        }

        // Ad-hoc spawn with provided parameters
        string? role = GetOptionalString(root, "role");
        int maxTokens = root.TryGetProperty("max_tokens", out var mt) ? mt.GetInt32() : 4096;
        float temperature = root.TryGetProperty("temperature", out var temp)
            ? (float)temp.GetDouble() : 0.5f;
        string systemPrompt = GetOptionalString(root, "system_prompt")
            ?? PromptTemplateLoader.GetPromptText("MeTTa", "DefaultAgentRole").Replace("{{$role}}", (role ?? "general").ToLowerInvariant());

        List<string>? capabilities = null;
        if (root.TryGetProperty("capabilities", out var capsElem))
        {
            capabilities = capsElem.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => s != null)
                .Cast<string>()
                .ToList();
        }

        var def = new MeTTaAgentDef(
            agentId, provider, model, role ?? "Custom",
            systemPrompt, maxTokens, temperature,
            Capabilities: capabilities);

        var adhocSpawnResult = await _runtime.SpawnAgentAsync(def, ct).ConfigureAwait(false);
        return adhocSpawnResult.IsSuccess
            ? Result<string, string>.Success($"Agent '{agentId}' spawned successfully")
            : Result<string, string>.Failure(adhocSpawnResult.Error);
    }

    private async Task<Result<string, string>> HandleTaskAsync(JsonElement root, CancellationToken ct)
    {
        string? agentId = GetOptionalString(root, "agent_id");
        string? prompt = GetOptionalString(root, "prompt");
        string taskId = GetOptionalString(root, "task_id")
            ?? $"task-{Interlocked.Increment(ref _taskCounter)}";

        if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(prompt))
            return Result<string, string>.Failure("Task requires: agent_id, prompt");

        return await _runtime.ExecuteTaskAsync(agentId, taskId, prompt, ct).ConfigureAwait(false);
    }

    private async Task<Result<string, string>> HandleRouteAsync(JsonElement root, CancellationToken ct)
    {
        string? capability = GetOptionalString(root, "capability");
        string? prompt = GetOptionalString(root, "prompt");
        string taskId = GetOptionalString(root, "task_id")
            ?? $"task-{Interlocked.Increment(ref _taskCounter)}";

        if (string.IsNullOrEmpty(capability) || string.IsNullOrEmpty(prompt))
            return Result<string, string>.Failure("Route requires: capability, prompt");

        return await _runtime.RouteTaskAsync(taskId, capability, prompt, ct).ConfigureAwait(false);
    }

    private async Task<Result<string, string>> HandlePipelineAsync(JsonElement root, CancellationToken ct)
    {
        string? prompt = GetOptionalString(root, "prompt");
        string taskId = GetOptionalString(root, "task_id")
            ?? $"pipeline-{Interlocked.Increment(ref _taskCounter)}";

        if (string.IsNullOrEmpty(prompt))
            return Result<string, string>.Failure("Pipeline requires: prompt");

        List<string>? agentIds = null;
        if (root.TryGetProperty("agent_ids", out var idsElem))
        {
            agentIds = idsElem.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => s != null)
                .Cast<string>()
                .ToList();
        }

        if (agentIds == null || agentIds.Count == 0)
        {
            // Try to get pipeline from MeTTa (escape prompt to prevent injection)
            string escapedPrompt = MeTTaParsingHelpers.EscapeMeTTaString(prompt);
            var pipelineResult = await _engine.ExecuteQueryAsync(
                $"!(code-pipeline \"{escapedPrompt}\")", ct).ConfigureAwait(false);

            if (pipelineResult.IsSuccess && !string.IsNullOrWhiteSpace(pipelineResult.Value))
            {
                agentIds = ParsePipelineAgents(pipelineResult.Value);
            }

            if (agentIds == null || agentIds.Count == 0)
                return Result<string, string>.Failure(
                    "Pipeline requires agent_ids or a matching MeTTa pipeline rule");
        }

        return await _runtime.ExecutePipelineAsync(taskId, agentIds, prompt, ct).ConfigureAwait(false);
    }

    private Result<string, string> HandleStatus()
    {
        var statuses = _runtime.GetAllStatuses();
        if (statuses.Count == 0)
            return Result<string, string>.Success("No agents spawned.");

        var json = JsonSerializer.Serialize(statuses, JsonDefaults.Indented);
        return Result<string, string>.Success(json);
    }

    private async Task<Result<string, string>> HandleTerminateAsync(JsonElement root, CancellationToken ct)
    {
        string? agentId = GetOptionalString(root, "agent_id");
        if (string.IsNullOrEmpty(agentId))
            return Result<string, string>.Failure("Terminate requires: agent_id");

        return await _runtime.TerminateAgentAsync(agentId, ct).ConfigureAwait(false);
    }

    private Result<string, string> HandleList()
    {
        return Result<string, string>.Success(_runtime.ListAgents());
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var elem)
            ? elem.GetString()
            : null;
    }

    private static List<string> ParsePipelineAgents(string mettaOutput)
    {
        var agents = new List<string>();
        // Match quoted strings inside (Pipeline ...) expression
        var matches = QuotedStringRegex().Matches(mettaOutput);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            agents.Add(match.Groups[1].Value);
        }
        return agents;
    }

    private static List<MeTTaAgentDef> ParseAgentDefsFromMeTTa(string mettaOutput)
    {
        var defs = new List<MeTTaAgentDef>();
        if (string.IsNullOrWhiteSpace(mettaOutput))
            return defs;

        // Use shared pattern from MeTTaParsingHelpers
        var matches = AgentDefRegex().Matches(mettaOutput);

        foreach (var groups in matches.Cast<System.Text.RegularExpressions.Match>()
            .Where(match => match.Groups.Count >= 8)
            .Select(match => match.Groups))
        {
            string agentId = groups[1].Value;
            string provider = groups[2].Value;
            string model = groups[3].Value;
            string role = groups[4].Value;
            string prompt = groups[5].Value;

            if (int.TryParse(groups[6].Value, out int maxTokens) &&
                float.TryParse(groups[7].Value, System.Globalization.CultureInfo.InvariantCulture, out float temperature))
            {
                defs.Add(new MeTTaAgentDef(
                    agentId, provider, model, role, prompt,
                    maxTokens, temperature));
            }
        }

        return defs;
    }

    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex QuotedStringRegex();

    [GeneratedRegex(MeTTaParsingHelpers.AgentDefPattern)]
    private static partial Regex AgentDefRegex();
}
