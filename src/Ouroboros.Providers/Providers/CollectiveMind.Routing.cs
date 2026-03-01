using Polly;
using Polly.CircuitBreaker;

namespace Ouroboros.Providers;

public sealed partial class CollectiveMind
{
    /// <summary>
    /// Adds a neural pathway (provider connection) to the collective.
    /// </summary>
    public CollectiveMind AddPathway(
        string name,
        ChatEndpointType endpointType,
        string? model = null,
        string? endpoint = null,
        string? apiKey = null,
        ChatRuntimeSettings? settings = null)
    {
        var (resolvedEndpoint, resolvedApiKey, _) = ChatConfig.ResolveWithOverrides(
            endpoint,
            apiKey,
            endpointType.ToString());

        resolvedEndpoint ??= ChatConfig.GetDefaultEndpoint(endpointType);
        model ??= GetDefaultModel(endpointType);

        var costTracker = new LlmCostTracker(model, name);

        // Create Polly circuit breaker for this pathway
        var circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (ex, duration) =>
                {
                    _thoughtStream.OnNext($"💔 Pathway '{name}' inhibited for {duration.TotalSeconds}s: {ex.Message}");
                },
                onReset: () =>
                {
                    _thoughtStream.OnNext($"💚 Pathway '{name}' restored");
                },
                onHalfOpen: () =>
                {
                    _thoughtStream.OnNext($"🔶 Probing pathway '{name}'...");
                });

        Ouroboros.Abstractions.Core.IChatCompletionModel chatModel = CreateModel(endpointType, resolvedEndpoint ?? "", resolvedApiKey ?? "", model, settings, costTracker);

        // Auto-detect tier based on endpoint type
        var tier = InferTier(endpointType, model);

        var pathway = new NeuralPathway
        {
            Name = name,
            EndpointType = endpointType,
            Model = chatModel,
            CostTracker = costTracker,
            CircuitBreaker = circuitBreaker,
            Tier = tier,
            Specializations = InferSpecializations(model)
        };

        lock (_lock)
        {
            _pathways.Add(pathway);
        }

        _thoughtStream.OnNext($"🧠 Neural pathway '{name}' connected ({endpointType}, tier={tier})");
        return this;
    }

    /// <summary>
    /// Sets the tier and specializations for a pathway.
    /// </summary>
    public CollectiveMind ConfigurePathway(string pathwayName, PathwayTier tier, params SubGoalType[] specializations)
    {
        lock (_lock)
        {
            var pathway = _pathways.FirstOrDefault(p => p.Name.Equals(pathwayName, StringComparison.OrdinalIgnoreCase));
            if (pathway != null)
            {
                pathway.Tier = tier;
                foreach (var spec in specializations)
                {
                    pathway.Specializations.Add(spec);
                }
                _thoughtStream.OnNext($"⚙️ Configured pathway '{pathwayName}' tier={tier} with {specializations.Length} specializations");
            }
        }
        return this;
    }

    private static PathwayTier InferTier(ChatEndpointType endpointType, string model)
    {
        if (endpointType == ChatEndpointType.OllamaLocal)
            return PathwayTier.Local;

        var modelLower = model.ToLowerInvariant();

        if (modelLower.Contains("opus") ||
            modelLower.Contains("gpt-4o") ||
            modelLower.Contains("claude-3-5") ||
            modelLower.Contains("claude-sonnet-4") ||
            modelLower.Contains("gemini-1.5-pro") ||
            modelLower.Contains("gemini-2.0"))
            return PathwayTier.CloudPremium;

        if (modelLower.Contains("codex") ||
            modelLower.Contains("deepseek-coder") ||
            modelLower.Contains("codellama") ||
            modelLower.Contains("starcoder"))
            return PathwayTier.Specialized;

        if (modelLower.Contains("mini") ||
            modelLower.Contains("haiku") ||
            modelLower.Contains("flash") ||
            modelLower.Contains("instant") ||
            modelLower.Contains("turbo"))
            return PathwayTier.CloudLight;

        return PathwayTier.CloudLight;
    }

    private static HashSet<SubGoalType> InferSpecializations(string model)
    {
        var specs = new HashSet<SubGoalType>();
        var modelLower = model.ToLowerInvariant();

        if (modelLower.Contains("code") || modelLower.Contains("coder"))
            specs.Add(SubGoalType.Coding);
        if (modelLower.Contains("math") || modelLower.Contains("wizard"))
            specs.Add(SubGoalType.Math);
        if (modelLower.Contains("creative") || modelLower.Contains("writer"))
            specs.Add(SubGoalType.Creative);

        return specs;
    }

    private static string GetDefaultModel(ChatEndpointType endpointType) => endpointType switch
    {
        ChatEndpointType.Anthropic => "claude-sonnet-4-20250514",
        ChatEndpointType.OpenAI => "gpt-4o",
        ChatEndpointType.DeepSeek => "deepseek-chat",
        ChatEndpointType.Groq => "llama-3.1-70b-versatile",
        ChatEndpointType.Google => "gemini-2.0-flash",
        ChatEndpointType.Mistral => "mistral-large",
        ChatEndpointType.OllamaLocal => "llama3.2",
        _ => "gpt-4o"
    };

    private static Ouroboros.Abstractions.Core.IChatCompletionModel CreateModel(
        ChatEndpointType endpointType,
        string endpoint,
        string apiKey,
        string model,
        ChatRuntimeSettings? settings,
        LlmCostTracker? costTracker)
    {
        return endpointType switch
        {
            ChatEndpointType.Anthropic => new AnthropicChatModel(apiKey, model, settings, costTracker: costTracker),
            ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey, model, settings, costTracker),
            ChatEndpointType.OllamaLocal => new OllamaCloudChatModel(endpoint, "ollama", model, settings, costTracker),
            ChatEndpointType.GitHubModels => new GitHubModelsChatModel(apiKey, model, endpoint, settings, costTracker),
            _ => new LiteLLMChatModel(endpoint, apiKey, model, settings, costTracker)
        };
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GenerateWithThinkingAsync(prompt, ct);
        return response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return ThinkingMode switch
        {
            CollectiveThinkingMode.Racing => await ThinkWithRacing(prompt, ct),
            CollectiveThinkingMode.Sequential => await ThinkSequentially(prompt, ct),
            CollectiveThinkingMode.Ensemble => await ThinkWithEnsemble(prompt, ct),
            CollectiveThinkingMode.Adaptive => await ThinkAdaptively(prompt, ct),
            CollectiveThinkingMode.Decomposed => await ThinkWithDecomposition(prompt, ct),
            _ => await ThinkSequentially(prompt, ct)
        };
    }

    /// <summary>
    /// Racing mode: Fire request to all healthy pathways, return first success.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithRacing(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🏎️ Racing mode: querying all pathways simultaneously...");

        var healthyPathways = _pathways.Where(p => p.IsHealthy).ToList();
        if (healthyPathways.Count == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = healthyPathways.Select(async pathway =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                return await pathway.CircuitBreaker.ExecuteAsync(async () =>
                {
                    var result = await QueryPathway(pathway, prompt, cts.Token);
                    sw.Stop();
                    pathway.RecordActivation(sw.Elapsed);
                    _thoughtStream.OnNext($"✓ '{pathway.Name}' responded in {sw.ElapsedMilliseconds}ms");
                    return (Pathway: pathway, Result: result, Success: true);
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ '{pathway.Name}' failed: {ex.Message}");
                return (Pathway: pathway, Result: new ThinkingResponse(null, ""), Success: false);
            }
        }).ToList();

        while (tasks.Count > 0)
        {
            var completed = await Task.WhenAny(tasks);
            tasks.Remove(completed);

            var result = await completed;
            if (result.Success && !string.IsNullOrEmpty(result.Result.Content))
            {
                cts.Cancel();
                AggregateCosts(result.Pathway);
                return result.Result;
            }
        }

        throw new InvalidOperationException("All neural pathways failed to produce a response");
    }

    /// <summary>
    /// Sequential mode: Round-robin with automatic failover.
    /// </summary>
    private async Task<ThinkingResponse> ThinkSequentially(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🔄 Sequential mode: round-robin with failover...");

        var triedPathways = new HashSet<NeuralPathway>();
        var errors = new List<string>();

        while (triedPathways.Count < _pathways.Count)
        {
            var pathway = GetNextPathway(triedPathways);
            if (pathway == null) break;

            triedPathways.Add(pathway);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var result = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                {
                    return await QueryPathway(pathway, prompt, ct);
                });

                sw.Stop();

                if (!string.IsNullOrEmpty(result.Content) && !result.Content.Contains("-fallback:"))
                {
                    pathway.RecordActivation(sw.Elapsed);
                    _thoughtStream.OnNext($"✓ '{pathway.Name}' responded in {sw.ElapsedMilliseconds}ms");
                    AggregateCosts(pathway);
                    return result;
                }

                pathway.RecordInhibition();
                errors.Add($"{pathway.Name}: empty/fallback response");
                _thoughtStream.OnNext($"⚠ '{pathway.Name}' returned empty/fallback response");
            }
            catch (BrokenCircuitException)
            {
                errors.Add($"{pathway.Name}: circuit open");
                _thoughtStream.OnNext($"⏸️ '{pathway.Name}' circuit is open, skipping...");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) // Intentional: circuit-breaker fallback across provider types
            {
                pathway.RecordInhibition();
                errors.Add($"{pathway.Name}: {ex.Message}");
                _thoughtStream.OnNext($"✗ '{pathway.Name}' failed: {ex.Message}");
            }
        }

        var detail = errors.Count > 0 ? " (" + string.Join("; ", errors) + ")" : "";
        throw new InvalidOperationException($"All neural pathways exhausted without successful response{detail}");
    }

    /// <summary>
    /// Adaptive mode: Selects strategy based on pathway health and query characteristics.
    /// </summary>
    private async Task<ThinkingResponse> ThinkAdaptively(string prompt, CancellationToken ct)
    {
        int healthyCount = HealthyPathwayCount;

        if (healthyCount == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        if (healthyCount == 1)
        {
            _thoughtStream.OnNext("🧠 Adaptive: Single pathway mode");
            return await ThinkSequentially(prompt, ct);
        }

        if (prompt.Length > 500 || prompt.Contains("analyze") || prompt.Contains("compare"))
        {
            _thoughtStream.OnNext("🧠 Adaptive: Complex query detected, using ensemble");
            return await ThinkWithEnsemble(prompt, ct);
        }

        if (prompt.Length < 100)
        {
            _thoughtStream.OnNext("🧠 Adaptive: Simple query detected, racing for speed");
            return await ThinkWithRacing(prompt, ct);
        }

        _thoughtStream.OnNext("🧠 Adaptive: Using balanced sequential mode");
        return await ThinkSequentially(prompt, ct);
    }
}
