// <copyright file="MeTTaAgentRuntime.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Reads agent definitions from MeTTa AtomSpace, spawns provider-backed
/// instances, executes tasks, and writes results back as MeTTa atoms.
/// This is the bridge between symbolic agent declarations and runtime execution.
/// </summary>
public sealed partial class MeTTaAgentRuntime : IAsyncDisposable
{
    private readonly IMeTTaEngine _engine;
    private readonly IReadOnlyList<IAgentProviderFactory> _providers;
    private readonly ConcurrentDictionary<string, SpawnedAgent> _agents = new();

    /// <summary>
    /// Creates a new MeTTa agent runtime.
    /// </summary>
    /// <param name="engine">The MeTTa engine for symbolic reasoning.</param>
    /// <param name="providers">Available provider factories.</param>
    public MeTTaAgentRuntime(
        IMeTTaEngine engine,
        IEnumerable<IAgentProviderFactory> providers)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>
    /// Gets all currently spawned agents.
    /// </summary>
    public IReadOnlyDictionary<string, SpawnedAgent> SpawnedAgents =>
        new Dictionary<string, SpawnedAgent>(_agents);

    /// <summary>
    /// Gets the MeTTa engine used by this runtime.
    /// </summary>
    public IMeTTaEngine Engine => _engine;

    /// <summary>
    /// Gets agents filtered by role.
    /// </summary>
    /// <param name="role">The role to filter by.</param>
    /// <returns>Spawned agents with the given role.</returns>
    public IEnumerable<SpawnedAgent> GetAgentsByRole(string role)
        => _agents.Values.Where(a =>
            a.Definition.Role.Equals(role, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Discovers all AgentDef atoms in the space and spawns agents
    /// that pass the (safe-to-spawn) and (can-spawn-more) rules.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of agents spawned.</returns>
    public async Task<Result<int, string>> SpawnAllAsync(CancellationToken ct = default)
    {
        // Query pattern: (match &self <condition> <result>)
        // Both condition and result use the same AgentDef pattern to ensure
        // we extract all fields from agents that pass the safety checks
        var result = await _engine.ExecuteQueryAsync(
            "!(match &self " +
            "  (and " +
            "    (AgentDef $id $prov $model $role $prompt $tokens $temp) " +
            "    (safe-to-spawn $id) " +
            "    (can-spawn-more)) " +
            "  (AgentDef $id $prov $model $role $prompt $tokens $temp))", ct);

        if (result.IsFailure)
            return Result<int, string>.Failure($"Failed to query agent defs: {result.Error}");

        var agentDefs = ParseAgentDefs(result.Value);
        int spawned = 0;

        foreach (var def in agentDefs)
        {
            var spawnResult = await SpawnAgentAsync(def, ct);
            if (spawnResult.IsSuccess) spawned++;
        }

        return Result<int, string>.Success(spawned);
    }

    /// <summary>
    /// Spawns a single agent from its MeTTa definition.
    /// </summary>
    /// <param name="def">The agent definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The spawned agent on success.</returns>
    public async Task<Result<SpawnedAgent, string>> SpawnAgentAsync(
        MeTTaAgentDef def, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(def.Provider));
        if (provider == null)
            return Result<SpawnedAgent, string>.Failure(
                $"No provider factory for '{def.Provider}'");

        var modelResult = await provider.CreateModelAsync(def, ct);
        if (modelResult.IsFailure)
            return Result<SpawnedAgent, string>.Failure(modelResult.Error);

        var agent = new SpawnedAgent(def, modelResult.Value, DateTime.UtcNow);
        
        // Use TryAdd to atomically check and add, preventing race conditions
        if (!_agents.TryAdd(def.AgentId, agent))
            return Result<SpawnedAgent, string>.Failure(
                $"Agent '{def.AgentId}' is already spawned");

        // Write spawn fact to MeTTa
        await _engine.AddFactAsync(
            $"(Spawned \"{def.AgentId}\" Active \"{DateTime.UtcNow:O}\")", ct);

        return Result<SpawnedAgent, string>.Success(agent);
    }

    /// <summary>
    /// Defines a new agent in MeTTa and optionally spawns it.
    /// </summary>
    /// <param name="def">The agent definition to register.</param>
    /// <param name="autoSpawn">Whether to spawn the agent immediately.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    public async Task<Result<string, string>> DefineAgentAsync(
        MeTTaAgentDef def, bool autoSpawn = false, CancellationToken ct = default)
    {
        // Write the AgentDef atom
        string defAtom = $"(AgentDef \"{def.AgentId}\" {def.Provider} \"{def.Model}\" {def.Role} " +
                         $"\"{EscapeMeTTa(def.SystemPrompt)}\" {def.MaxTokens} {def.Temperature})";

        var addResult = await _engine.AddFactAsync(defAtom, ct);
        if (addResult.IsFailure)
            return Result<string, string>.Failure($"Failed to add agent def: {addResult.Error}");

        // Write capability atoms
        if (def.Capabilities != null)
        {
            foreach (var cap in def.Capabilities)
            {
                var capResult = await _engine.AddFactAsync(
                    $"(HasCapability \"{def.AgentId}\" {cap})", ct);
                if (capResult.IsFailure)
                    return Result<string, string>.Failure(
                        $"Failed to add capability '{cap}' for agent '{def.AgentId}': {capResult.Error}");
            }
        }

        if (autoSpawn)
        {
            var spawnResult = await SpawnAgentAsync(def, ct);
            if (spawnResult.IsFailure)
                return Result<string, string>.Failure(spawnResult.Error);
        }

        return Result<string, string>.Success(
            $"Agent '{def.AgentId}' defined{(autoSpawn ? " and spawned" : "")} successfully");
    }

    /// <summary>
    /// Executes a task on a specific agent and writes the result back to MeTTa.
    /// </summary>
    /// <param name="agentId">The agent to execute on.</param>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="prompt">The task prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's response on success.</returns>
    public async Task<Result<string, string>> ExecuteTaskAsync(
        string agentId, string taskId, string prompt,
        CancellationToken ct = default)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
            return Result<string, string>.Failure($"Agent '{agentId}' not spawned");

        // Record assignment in MeTTa
        await _engine.AddFactAsync(
            $"(AssignedTask \"{taskId}\" \"{agentId}\" \"{EscapeMeTTa(Truncate(prompt, 200))}\")", ct);

        try
        {
            string fullPrompt = $"{agent.Definition.SystemPrompt}\n\n{prompt}";
            string response = await agent.Model.GenerateTextAsync(fullPrompt, ct);

            // Record result in MeTTa
            await _engine.AddFactAsync(
                $"(AgentResult \"{agentId}\" \"{taskId}\" Success " +
                $"\"{EscapeMeTTa(Truncate(response, 500))}\")", ct);

            return Result<string, string>.Success(response);
        }
        catch (Exception ex)
        {
            await _engine.AddFactAsync(
                $"(AgentResult \"{agentId}\" \"{taskId}\" Failed " +
                $"\"{EscapeMeTTa(ex.Message)}\")", ct);

            await _engine.AddFactAsync(
                $"(Spawned \"{agentId}\" Failed \"{DateTime.UtcNow:O}\")", ct);

            return Result<string, string>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Routes a task to the best available agent for a capability,
    /// using MeTTa rules for selection.
    /// </summary>
    /// <param name="taskId">Unique task identifier.</param>
    /// <param name="capability">The required capability.</param>
    /// <param name="prompt">The task prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's response on success.</returns>
    public async Task<Result<string, string>> RouteTaskAsync(
        string taskId, string capability, string prompt,
        CancellationToken ct = default)
    {
        // Validate capability is a valid symbol (alphanumeric and underscores only)
        if (!IsValidMeTTaSymbol(capability))
            return Result<string, string>.Failure(
                $"Invalid capability format: '{capability}'. Must be alphanumeric with underscores only.");

        // Ask MeTTa which agent should handle this
        var queryResult = await _engine.ExecuteQueryAsync(
            $"!(agent-for-capability {capability})", ct);

        if (queryResult.IsSuccess && !string.IsNullOrWhiteSpace(queryResult.Value))
        {
            string agentId = queryResult.Value.Trim().Trim('"');
            if (_agents.ContainsKey(agentId))
                return await ExecuteTaskAsync(agentId, taskId, prompt, ct);
        }

        // Fallback: find any spawned agent with matching capability
        var fallbackAgent = _agents.Values.FirstOrDefault(a =>
            a.Definition.Capabilities?.Contains(capability) == true);

        if (fallbackAgent != null)
            return await ExecuteTaskAsync(fallbackAgent.Definition.AgentId, taskId, prompt, ct);

        return Result<string, string>.Failure(
            $"No agent available for capability '{capability}'");
    }

    /// <summary>
    /// Executes a multi-agent pipeline: each agent's output feeds into the next.
    /// </summary>
    /// <param name="taskId">Base task identifier.</param>
    /// <param name="agentIds">Ordered list of agent IDs to execute.</param>
    /// <param name="prompt">Initial prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The final agent's response on success.</returns>
    public async Task<Result<string, string>> ExecutePipelineAsync(
        string taskId, IReadOnlyList<string> agentIds, string prompt,
        CancellationToken ct = default)
    {
        if (agentIds == null || agentIds.Count == 0)
            return Result<string, string>.Failure("Pipeline requires at least one agent");

        string currentInput = prompt;

        for (int i = 0; i < agentIds.Count; i++)
        {
            string agentId = agentIds[i];
            string stepTaskId = $"{taskId}-step{i}-{agentId}";

            var stepResult = await ExecuteTaskAsync(agentId, stepTaskId, currentInput, ct);
            if (stepResult.IsFailure)
                return stepResult;

            // Record inter-agent message
            if (i < agentIds.Count - 1)
            {
                await _engine.AddFactAsync(
                    $"(AgentMessage \"{agentId}\" \"{agentIds[i + 1]}\" " +
                    $"\"{stepTaskId}\" \"{EscapeMeTTa(Truncate(stepResult.Value, 200))}\")", ct);
            }

            currentInput = stepResult.Value;
        }

        return Result<string, string>.Success(currentInput);
    }

    /// <summary>
    /// Terminates a specific agent and records the event in MeTTa.
    /// </summary>
    /// <param name="agentId">The agent to terminate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure.</returns>
    public async Task<Result<string, string>> TerminateAgentAsync(
        string agentId, CancellationToken ct = default)
    {
        if (!_agents.TryRemove(agentId, out _))
            return Result<string, string>.Failure($"Agent '{agentId}' not found");

        await _engine.AddFactAsync(
            $"(Spawned \"{agentId}\" Terminated \"{DateTime.UtcNow:O}\")", ct);

        return Result<string, string>.Success($"Agent '{agentId}' terminated");
    }

    /// <summary>
    /// Gets the status of all spawned agents.
    /// </summary>
    /// <returns>List of agent statuses.</returns>
    public IReadOnlyList<AgentOperationStatus> GetAllStatuses()
    {
        return _agents.Values.Select(a => new AgentOperationStatus(
            a.Definition.AgentId,
            "Active",
            $"Provider={a.Definition.Provider}, Model={a.Definition.Model}, Role={a.Definition.Role}",
            a.SpawnedAt)).ToList();
    }

    /// <summary>
    /// Lists all defined agents (from MeTTa) and their spawn status.
    /// </summary>
    /// <returns>Formatted agent list.</returns>
    public string ListAgents()
    {
        if (_agents.IsEmpty)
            return "No agents spawned.";

        var lines = _agents.Values.Select(a =>
            $"- {a.Definition.AgentId}: provider={a.Definition.Provider}, " +
            $"model={a.Definition.Model}, role={a.Definition.Role}, " +
            $"spawned={a.SpawnedAt:O}");

        return string.Join("\n", lines);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var agent in _agents.Values)
        {
            await _engine.AddFactAsync(
                $"(Spawned \"{agent.Definition.AgentId}\" Terminated \"{DateTime.UtcNow:O}\")");
        }
        _agents.Clear();
    }

    // --- Helpers ---

    private List<MeTTaAgentDef> ParseAgentDefs(string mettaOutput)
    {
        var defs = new List<MeTTaAgentDef>();
        if (string.IsNullOrWhiteSpace(mettaOutput))
            return defs;

        // Match patterns like: (AgentDef "id" Provider "model" Role "prompt" tokens temp)
        var matches = AgentDefRegex().Matches(mettaOutput);

        foreach (Match match in matches)
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

    private static string EscapeMeTTa(string text)
        => text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";

    private static bool IsValidMeTTaSymbol(string symbol)
    {
        return !string.IsNullOrWhiteSpace(symbol) && MeTTaSymbolRegex().IsMatch(symbol);
    }

    [GeneratedRegex(
        MeTTaParsingHelpers.AgentDefPattern,
        RegexOptions.Compiled)]
    private static partial Regex AgentDefRegex();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled)]
    private static partial Regex MeTTaSymbolRegex();
}
