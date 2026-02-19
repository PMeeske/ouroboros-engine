// <copyright file="MeTTaAgentTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Tool that allows the orchestrating LLM to define, spawn, and task sub-agents
/// by writing MeTTa atoms. The LLM speaks MeTTa to control agents.
/// </summary>
public sealed class MeTTaAgentTool : ITool
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
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
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
                "define" => await HandleDefineAsync(root, ct),
                "spawn" => await HandleSpawnAsync(root, ct),
                "task" => await HandleTaskAsync(root, ct),
                "route" => await HandleRouteAsync(root, ct),
                "pipeline" => await HandlePipelineAsync(root, ct),
                "status" => HandleStatus(),
                "terminate" => await HandleTerminateAsync(root, ct),
                "list" => HandleList(),
                _ => Result<string, string>.Failure($"Unknown operation: {operation}")
            };
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Invalid JSON input: {ex.Message}");
        }
        catch (Exception ex)
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
            systemPrompt ?? $"You are a {role.ToLowerInvariant()} agent.",
            maxTokens, temperature,
            Capabilities: capabilities);

        return await _runtime.DefineAgentAsync(def, autoSpawn: false, ct);
    }

    private async Task<Result<string, string>> HandleSpawnAsync(JsonElement root, CancellationToken ct)
    {
        string? agentId = GetOptionalString(root, "agent_id");

        if (string.IsNullOrEmpty(agentId))
        {
            // Spawn all defined agents
            var result = await _runtime.SpawnAllAsync(ct);
            return result.IsSuccess
                ? Result<string, string>.Success($"Spawned {result.Value} agents")
                : Result<string, string>.Failure(result.Error);
        }

        // Try to get existing AgentDef from MeTTa first
        string? provider = GetOptionalString(root, "provider");
        string? model = GetOptionalString(root, "model");

        if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model))
        {
            // Query MeTTa for the existing definition
            var queryResult = await _engine.ExecuteQueryAsync(
                $"!(match &self (AgentDef \"{agentId}\" $prov $model $role $prompt $tokens $temp) " +
                $"(AgentDef \"{agentId}\" $prov $model $role $prompt $tokens $temp))", ct);

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

            var spawnResult = await _runtime.SpawnAgentAsync(defs[0], ct);
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
            ?? $"You are a {(role ?? "general").ToLowerInvariant()} agent.";

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

        var adhocSpawnResult = await _runtime.SpawnAgentAsync(def, ct);
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

        return await _runtime.ExecuteTaskAsync(agentId, taskId, prompt, ct);
    }

    private async Task<Result<string, string>> HandleRouteAsync(JsonElement root, CancellationToken ct)
    {
        string? capability = GetOptionalString(root, "capability");
        string? prompt = GetOptionalString(root, "prompt");
        string taskId = GetOptionalString(root, "task_id")
            ?? $"task-{Interlocked.Increment(ref _taskCounter)}";

        if (string.IsNullOrEmpty(capability) || string.IsNullOrEmpty(prompt))
            return Result<string, string>.Failure("Route requires: capability, prompt");

        return await _runtime.RouteTaskAsync(taskId, capability, prompt, ct);
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
            string escapedPrompt = prompt.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var pipelineResult = await _engine.ExecuteQueryAsync(
                $"!(code-pipeline \"{escapedPrompt}\")", ct);

            if (pipelineResult.IsSuccess && !string.IsNullOrWhiteSpace(pipelineResult.Value))
            {
                agentIds = ParsePipelineAgents(pipelineResult.Value);
            }

            if (agentIds == null || agentIds.Count == 0)
                return Result<string, string>.Failure(
                    "Pipeline requires agent_ids or a matching MeTTa pipeline rule");
        }

        return await _runtime.ExecutePipelineAsync(taskId, agentIds, prompt, ct);
    }

    private Result<string, string> HandleStatus()
    {
        var statuses = _runtime.GetAllStatuses();
        if (statuses.Count == 0)
            return Result<string, string>.Success("No agents spawned.");

        var json = JsonSerializer.Serialize(statuses, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Result<string, string>.Success(json);
    }

    private async Task<Result<string, string>> HandleTerminateAsync(JsonElement root, CancellationToken ct)
    {
        string? agentId = GetOptionalString(root, "agent_id");
        if (string.IsNullOrEmpty(agentId))
            return Result<string, string>.Failure("Terminate requires: agent_id");

        return await _runtime.TerminateAgentAsync(agentId, ct);
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
        var matches = System.Text.RegularExpressions.Regex.Matches(
            mettaOutput, @"""([^""]+)""");
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

        // Match patterns like: (AgentDef "id" Provider "model" Role "prompt" tokens temp)
        var pattern = @"\(AgentDef\s+""([^""]+)""\s+(\w+)\s+""([^""]+)""\s+(\w+)\s+""([^""]*)""\s+(\d+)\s+([\d.]+)\)";
        var matches = System.Text.RegularExpressions.Regex.Matches(mettaOutput, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count >= 8)
            {
                string agentId = match.Groups[1].Value;
                string provider = match.Groups[2].Value;
                string model = match.Groups[3].Value;
                string role = match.Groups[4].Value;
                string prompt = match.Groups[5].Value;

                if (int.TryParse(match.Groups[6].Value, out int maxTokens) &&
                    float.TryParse(match.Groups[7].Value, System.Globalization.CultureInfo.InvariantCulture, out float temperature))
                {
                    defs.Add(new MeTTaAgentDef(
                        agentId, provider, model, role, prompt,
                        maxTokens, temperature));
                }
            }
        }

        return defs;
    }
}
