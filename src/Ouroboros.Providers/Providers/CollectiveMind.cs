#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using Polly;
using Polly.CircuitBreaker;

namespace Ouroboros.Providers;

/// <summary>
/// A unified conscious mind that emerges from multiple LLM providers.
/// Uses Polly for resilience and Rx for reactive streaming.
/// Presents itself as ONE coherent intelligence while leveraging the collective.
/// Supports goal decomposition and intelligent task routing across pathways.
/// </summary>
public sealed class CollectiveMind : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
{
    private readonly List<NeuralPathway> _pathways = new();
    private readonly object _lock = new();
    private readonly Subject<string> _thoughtStream = new();
    private readonly LlmCostTracker _collectiveCostTracker;
    private int _currentPathwayIndex;
    private NeuralPathway? _masterPathway;
    private MasterModelElection? _election;
    private DecompositionConfig _decompositionConfig = DecompositionConfig.Default;
    private readonly Subject<SubGoalResult> _subGoalStream = new();
    private readonly IITPhiCalculator _phiCalculator = new();

    /// <summary>
    /// Observable stream of the mind's internal thoughts and reasoning.
    /// </summary>
    public IObservable<string> ThoughtStream => _thoughtStream.AsObservable();

    /// <summary>
    /// Observable stream of sub-goal execution results.
    /// </summary>
    public IObservable<SubGoalResult> SubGoalStream => _subGoalStream.AsObservable();

    /// <summary>
    /// Configuration for goal decomposition behavior.
    /// </summary>
    public DecompositionConfig DecompositionConfig
    {
        get => _decompositionConfig;
        set => _decompositionConfig = value ?? DecompositionConfig.Default;
    }

    /// <summary>
    /// The collective's thinking mode.
    /// </summary>
    public CollectiveThinkingMode ThinkingMode { get; set; } = CollectiveThinkingMode.Adaptive;

    /// <summary>
    /// Election strategy for selecting best answers.
    /// </summary>
    public ElectionStrategy ElectionStrategy
    {
        get => _election?.Strategy ?? ElectionStrategy.WeightedMajority;
        set { if (_election != null) _election.Strategy = value; }
    }

    /// <summary>
    /// Observable stream of election events.
    /// </summary>
    public IObservable<ElectionEvent>? ElectionEvents => _election?.ElectionEvents;

    /// <summary>
    /// Gets optimization suggestions from the election system.
    /// </summary>
    public IReadOnlyList<OptimizationSuggestion> GetOptimizationSuggestions() =>
        _election?.GetOptimizationSuggestions() ?? Array.Empty<OptimizationSuggestion>();

    /// <summary>
    /// Gets the aggregate cost tracker for the collective mind.
    /// </summary>
    public LlmCostTracker? CostTracker => _collectiveCostTracker;

    /// <summary>
    /// Gets all neural pathways.
    /// </summary>
    public IReadOnlyList<NeuralPathway> Pathways
    {
        get { lock (_lock) return _pathways.ToList(); }
    }

    /// <summary>
    /// Gets the number of healthy pathways.
    /// </summary>
    public int HealthyPathwayCount
    {
        get { lock (_lock) return _pathways.Count(p => p.IsHealthy); }
    }

    public CollectiveMind(ElectionStrategy electionStrategy = ElectionStrategy.WeightedMajority)
    {
        _collectiveCostTracker = new LlmCostTracker("collective-mind", "Collective");
        _election = new MasterModelElection(null, electionStrategy);
    }

    /// <summary>
    /// Designates a pathway as the master orchestrator.
    /// The master evaluates and selects best answers from other pathways.
    /// </summary>
    public CollectiveMind SetMaster(string pathwayName)
    {
        lock (_lock)
        {
            _masterPathway = _pathways.FirstOrDefault(p => p.Name.Equals(pathwayName, StringComparison.OrdinalIgnoreCase));
            if (_masterPathway != null)
            {
                _election?.Dispose();
                _election = new MasterModelElection(_masterPathway, ElectionStrategy);
                _thoughtStream.OnNext($"👑 Master pathway set: {pathwayName}");
            }
        }
        return this;
    }

    /// <summary>
    /// Sets the first pathway as the master orchestrator.
    /// </summary>
    public CollectiveMind SetFirstAsMaster()
    {
        lock (_lock)
        {
            _masterPathway = _pathways.FirstOrDefault();
            if (_masterPathway != null)
            {
                _election?.Dispose();
                _election = new MasterModelElection(_masterPathway, ElectionStrategy);
                _thoughtStream.OnNext($"👑 Master pathway set: {_masterPathway.Name}");
            }
        }
        return this;
    }

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
                // Since NeuralPathway uses init properties, we need to recreate or use reflection
                // For now, we'll note that Tier is set at creation time
                // But we can add specializations
                foreach (var spec in specializations)
                {
                    pathway.Specializations.Add(spec);
                }
                _thoughtStream.OnNext($"⚙️ Configured pathway '{pathwayName}' with {specializations.Length} specializations");
            }
        }
        return this;
    }

    private static PathwayTier InferTier(ChatEndpointType endpointType, string model)
    {
        // Local inference
        if (endpointType == ChatEndpointType.OllamaLocal)
            return PathwayTier.Local;

        // Cloud - check model name for tier hints
        var modelLower = model.ToLowerInvariant();

        // Premium models
        if (modelLower.Contains("opus") ||
            modelLower.Contains("gpt-4o") ||
            modelLower.Contains("claude-3-5") ||
            modelLower.Contains("claude-sonnet-4") ||
            modelLower.Contains("gemini-1.5-pro") ||
            modelLower.Contains("gemini-2.0"))
            return PathwayTier.CloudPremium;

        // Specialized coding models
        if (modelLower.Contains("codex") ||
            modelLower.Contains("deepseek-coder") ||
            modelLower.Contains("codellama") ||
            modelLower.Contains("starcoder"))
            return PathwayTier.Specialized;

        // Light models
        if (modelLower.Contains("mini") ||
            modelLower.Contains("haiku") ||
            modelLower.Contains("flash") ||
            modelLower.Contains("instant") ||
            modelLower.Contains("turbo"))
            return PathwayTier.CloudLight;

        // Default to light for unknown
        return PathwayTier.CloudLight;
    }

    private static HashSet<SubGoalType> InferSpecializations(string model)
    {
        var specs = new HashSet<SubGoalType>();
        var modelLower = model.ToLowerInvariant();

        if (modelLower.Contains("code") || modelLower.Contains("coder"))
        {
            specs.Add(SubGoalType.Coding);
        }
        if (modelLower.Contains("math") || modelLower.Contains("wizard"))
        {
            specs.Add(SubGoalType.Math);
        }
        if (modelLower.Contains("creative") || modelLower.Contains("writer"))
        {
            specs.Add(SubGoalType.Creative);
        }

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
        // Return only the content — election thinking/metadata is already
        // streamed to the UI via ThoughtStream (SplitConsole upper pane).
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

        // Wait for first successful result
        while (tasks.Count > 0)
        {
            var completed = await Task.WhenAny(tasks);
            tasks.Remove(completed);

            var result = await completed;
            if (result.Success && !string.IsNullOrEmpty(result.Result.Content))
            {
                cts.Cancel(); // Cancel remaining tasks
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
                _thoughtStream.OnNext($"⚠ '{pathway.Name}' returned empty/fallback response");
            }
            catch (BrokenCircuitException)
            {
                _thoughtStream.OnNext($"⏸️ '{pathway.Name}' circuit is open, skipping...");
            }
            catch (Exception ex)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ '{pathway.Name}' failed: {ex.Message}");
            }
        }

        throw new InvalidOperationException("All neural pathways exhausted without successful response");
    }

    /// <summary>
    /// Ensemble mode: Query multiple providers and elect the best response via master orchestration.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithEnsemble(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🎭 Ensemble mode: gathering perspectives from multiple pathways...");

        // Exclude master from worker pathways to avoid self-evaluation
        var workerPathways = _pathways
            .Where(p => p.IsHealthy && p != _masterPathway)
            .Take(5)
            .ToList();

        if (workerPathways.Count == 0)
        {
            // Fall back to all healthy pathways including master
            workerPathways = _pathways.Where(p => p.IsHealthy).Take(3).ToList();
        }

        if (workerPathways.Count == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        // Query all worker pathways in parallel
        var tasks = workerPathways.Select(async pathway =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                    await QueryPathway(pathway, prompt, ct));
                sw.Stop();
                pathway.RecordActivation(sw.Elapsed);

                return ResponseCandidate<ThinkingResponse>.Create(result, pathway.Name, sw.Elapsed);
            }
            catch
            {
                pathway.RecordInhibition();
                return ResponseCandidate<ThinkingResponse>.Invalid(pathway.Name);
            }
        });

        var candidates = (await Task.WhenAll(tasks)).ToList();
        var validCandidates = candidates.Where(c => c.IsValid && !string.IsNullOrEmpty(c.Value.Content)).ToList();

        if (validCandidates.Count == 0)
            throw new InvalidOperationException("No pathways returned valid responses");

        // Single valid response - return directly
        if (validCandidates.Count == 1)
        {
            var solo = validCandidates[0];
            var pathway = _pathways.First(p => p.Name == solo.Source);
            AggregateCosts(pathway);
            return solo.Value;
        }

        // Multiple responses - run election with master orchestration
        _thoughtStream.OnNext($"🗳️ Running election with {validCandidates.Count} candidates...");

        if (_election != null)
        {
            var electionResult = await _election.RunElectionAsync(validCandidates, prompt, ct);

            // Aggregate costs from all queried pathways
            foreach (var c in validCandidates)
            {
                var pathway = _pathways.FirstOrDefault(p => p.Name == c.Source);
                if (pathway != null) AggregateCosts(pathway);
            }

            // Build thinking trace with election details
            var synthesis = new StringBuilder();
            synthesis.AppendLine($"🗳️ Election Results ({electionResult.Strategy}):");
            synthesis.AppendLine($"   {electionResult.Rationale}");
            synthesis.AppendLine();
            foreach (var (source, votes) in electionResult.Votes.OrderByDescending(kv => kv.Value))
            {
                string marker = source == electionResult.Winner.Source ? "→" : " ";
                synthesis.AppendLine($"   {marker} {source}: {votes:F3}");
            }

            _thoughtStream.OnNext($"👑 Winner: {electionResult.Winner.Source}");

            return new ThinkingResponse(synthesis.ToString(), electionResult.Winner.Value.Content);
        }

        // Fallback: select by pathway weight if no election system
        var best = validCandidates
            .Select(c => (Candidate: c, Pathway: _pathways.First(p => p.Name == c.Source)))
            .OrderByDescending(x => x.Pathway.Weight * x.Pathway.ActivationRate)
            .First();

        foreach (var c in validCandidates)
        {
            var pathway = _pathways.FirstOrDefault(p => p.Name == c.Source);
            if (pathway != null) AggregateCosts(pathway);
        }

        return best.Candidate.Value;
    }

    /// <summary>
    /// Adaptive mode: Selects strategy based on pathway health and query characteristics.
    /// </summary>
    private async Task<ThinkingResponse> ThinkAdaptively(string prompt, CancellationToken ct)
    {
        int healthyCount = HealthyPathwayCount;

        if (healthyCount == 0)
            throw new InvalidOperationException("No healthy neural pathways available");

        // Choose strategy based on conditions
        if (healthyCount == 1)
        {
            _thoughtStream.OnNext("🧠 Adaptive: Single pathway mode");
            return await ThinkSequentially(prompt, ct);
        }

        // For long/complex prompts, use ensemble for quality
        if (prompt.Length > 500 || prompt.Contains("analyze") || prompt.Contains("compare"))
        {
            _thoughtStream.OnNext("🧠 Adaptive: Complex query detected, using ensemble");
            return await ThinkWithEnsemble(prompt, ct);
        }

        // For simple queries, race for speed
        if (prompt.Length < 100)
        {
            _thoughtStream.OnNext("🧠 Adaptive: Simple query detected, racing for speed");
            return await ThinkWithRacing(prompt, ct);
        }

        // Default to sequential for balanced approach
        _thoughtStream.OnNext("🧠 Adaptive: Using balanced sequential mode");
        return await ThinkSequentially(prompt, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════════════
    // GOAL DECOMPOSITION - Intelligent task routing across local/cloud pathways
    // ═══════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Decomposed mode: Split request into sub-goals and route to optimal pathways.
    /// </summary>
    private async Task<ThinkingResponse> ThinkWithDecomposition(string prompt, CancellationToken ct)
    {
        _thoughtStream.OnNext("🎯 Decomposed mode: Analyzing request for sub-goals...");

        // Step 1: Use master or best pathway to decompose the goal
        var decomposer = _masterPathway ?? GetBestPathwayForDecomposition();
        if (decomposer == null)
            throw new InvalidOperationException("No pathways available for goal decomposition");

        var subGoals = await DecomposeIntoSubGoals(decomposer, prompt, ct);

        if (subGoals.Count == 0 || (subGoals.Count == 1 && subGoals[0].Complexity <= _decompositionConfig.DecompositionThreshold))
        {
            _thoughtStream.OnNext("🎯 Request is simple enough - executing directly");
            return await ThinkSequentially(prompt, ct);
        }

        _thoughtStream.OnNext($"🎯 Decomposed into {subGoals.Count} sub-goals");

        // Step 2: Route and execute sub-goals
        var results = await ExecuteSubGoalsAsync(subGoals, ct);

        // Step 3: Synthesize results
        var synthesis = await SynthesizeResultsAsync(decomposer, prompt, subGoals, results, ct);

        return synthesis;
    }

    private NeuralPathway? GetBestPathwayForDecomposition()
    {
        lock (_lock)
        {
            // Prefer premium cloud for decomposition (needs good reasoning)
            return _pathways
                .Where(p => p.IsHealthy)
                .OrderByDescending(p => p.Tier == PathwayTier.CloudPremium ? 10 : 0)
                .ThenByDescending(p => p.Weight * p.ActivationRate)
                .FirstOrDefault();
        }
    }

    private async Task<List<SubGoal>> DecomposeIntoSubGoals(NeuralPathway decomposer, string prompt, CancellationToken ct)
    {
        var decompositionPrompt = $"""
            Analyze this request and decompose it into sub-goals. Return a JSON array of sub-goals.
            Each sub-goal should have:
            - id: unique identifier (e.g., "goal_1")
            - description: what needs to be done
            - complexity: "trivial", "simple", "moderate", "complex", or "expert"
            - type: "retrieval", "transform", "reasoning", "creative", "coding", "math", or "synthesis"
            - dependencies: array of goal ids this depends on (empty if none)

            Request: {prompt}

            Return ONLY a JSON array, no explanation:
            """;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await decomposer.CircuitBreaker.ExecuteAsync(async () =>
                await QueryPathway(decomposer, decompositionPrompt, ct));
            sw.Stop();
            decomposer.RecordActivation(sw.Elapsed);

            return ParseSubGoals(response.Content);
        }
        catch (Exception ex)
        {
            _thoughtStream.OnNext($"⚠️ Decomposition failed: {ex.Message}, falling back to single goal");
            decomposer.RecordInhibition();

            // Return single goal representing the entire request
            return new List<SubGoal>
            {
                new(
                    Id: "goal_1",
                    Description: prompt,
                    Complexity: EstimateComplexity(prompt),
                    Type: EstimateGoalType(prompt),
                    Dependencies: Array.Empty<string>(),
                    PreferredTier: PathwayTier.CloudLight)
            };
        }
    }

    private List<SubGoal> ParseSubGoals(string json)
    {
        var goals = new List<SubGoal>();

        try
        {
            // Extract JSON array from response
            var jsonMatch = Regex.Match(json, @"\[[\s\S]*\]");
            if (!jsonMatch.Success)
            {
                _thoughtStream.OnNext("⚠️ No JSON array found in decomposition response");
                return goals;
            }

            var jsonArray = System.Text.Json.JsonDocument.Parse(jsonMatch.Value);

            foreach (var element in jsonArray.RootElement.EnumerateArray())
            {
                var id = element.GetProperty("id").GetString() ?? $"goal_{goals.Count + 1}";
                var description = element.GetProperty("description").GetString() ?? "";
                var complexityStr = element.TryGetProperty("complexity", out var c) ? c.GetString() : "moderate";
                var typeStr = element.TryGetProperty("type", out var t) ? t.GetString() : "reasoning";
                var deps = element.TryGetProperty("dependencies", out var d)
                    ? d.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                    : new List<string>();

                var complexity = ParseComplexity(complexityStr);
                var type = ParseGoalType(typeStr);
                var tier = DeterminePreferredTier(complexity, type);

                goals.Add(new SubGoal(id, description, complexity, type, deps, tier));
            }
        }
        catch (Exception ex)
        {
            _thoughtStream.OnNext($"⚠️ Failed to parse sub-goals: {ex.Message}");
        }

        return goals.Take(_decompositionConfig.MaxSubGoals).ToList();
    }

    private SubGoalComplexity ParseComplexity(string? s) => s?.ToLowerInvariant() switch
    {
        "trivial" => SubGoalComplexity.Trivial,
        "simple" => SubGoalComplexity.Simple,
        "moderate" => SubGoalComplexity.Moderate,
        "complex" => SubGoalComplexity.Complex,
        "expert" => SubGoalComplexity.Expert,
        _ => SubGoalComplexity.Moderate
    };

    private SubGoalType ParseGoalType(string? s) => s?.ToLowerInvariant() switch
    {
        "retrieval" => SubGoalType.Retrieval,
        "transform" => SubGoalType.Transform,
        "reasoning" => SubGoalType.Reasoning,
        "creative" => SubGoalType.Creative,
        "coding" or "code" => SubGoalType.Coding,
        "math" or "mathematical" => SubGoalType.Math,
        "synthesis" or "synthesize" => SubGoalType.Synthesis,
        _ => SubGoalType.Reasoning
    };

    private SubGoalComplexity EstimateComplexity(string text)
    {
        var length = text.Length;
        var questionCount = Regex.Matches(text, @"\?").Count;
        var hasMultipleSteps = Regex.IsMatch(text, @"\b(then|next|after|finally|also|and then)\b", RegexOptions.IgnoreCase);

        if (length < 50 && questionCount <= 1) return SubGoalComplexity.Simple;
        if (length < 200 && !hasMultipleSteps) return SubGoalComplexity.Moderate;
        if (length < 500) return SubGoalComplexity.Complex;
        return SubGoalComplexity.Expert;
    }

    private SubGoalType EstimateGoalType(string text)
    {
        var lower = text.ToLowerInvariant();

        if (Regex.IsMatch(lower, @"\b(code|program|function|class|implement|debug|refactor)\b"))
            return SubGoalType.Coding;
        if (Regex.IsMatch(lower, @"\b(calculate|compute|solve|equation|formula|math)\b"))
            return SubGoalType.Math;
        if (Regex.IsMatch(lower, @"\b(write|create|compose|generate|story|poem|creative)\b"))
            return SubGoalType.Creative;
        if (Regex.IsMatch(lower, @"\b(analyze|compare|evaluate|reason|explain why)\b"))
            return SubGoalType.Reasoning;
        if (Regex.IsMatch(lower, @"\b(convert|transform|format|translate|summarize)\b"))
            return SubGoalType.Transform;
        if (Regex.IsMatch(lower, @"\b(find|search|lookup|what is|who is|when)\b"))
            return SubGoalType.Retrieval;

        return SubGoalType.Reasoning;
    }

    private PathwayTier DeterminePreferredTier(SubGoalComplexity complexity, SubGoalType type)
    {
        // Check type-specific routing first
        if (_decompositionConfig.TypeRouting.TryGetValue(type, out var typeTier))
        {
            // Override with complexity if needed
            if (complexity <= SubGoalComplexity.Simple && _decompositionConfig.PreferLocalForSimple)
                return PathwayTier.Local;
            return typeTier;
        }

        // Complexity-based fallback
        return complexity switch
        {
            SubGoalComplexity.Trivial => PathwayTier.Local,
            SubGoalComplexity.Simple => _decompositionConfig.PreferLocalForSimple ? PathwayTier.Local : PathwayTier.CloudLight,
            SubGoalComplexity.Moderate => PathwayTier.CloudLight,
            SubGoalComplexity.Complex => PathwayTier.CloudPremium,
            SubGoalComplexity.Expert => PathwayTier.CloudPremium,
            _ => PathwayTier.CloudLight
        };
    }

    private async Task<Dictionary<string, SubGoalResult>> ExecuteSubGoalsAsync(
        List<SubGoal> goals,
        CancellationToken ct)
    {
        var results = new ConcurrentDictionary<string, SubGoalResult>();
        var completed = new HashSet<string>();

        // Build dependency graph
        var dependencyGraph = goals.ToDictionary(g => g.Id, g => g.Dependencies.ToList());

        // Execute in waves based on dependencies
        while (completed.Count < goals.Count)
        {
            // Find goals ready to execute (all dependencies satisfied)
            var ready = goals
                .Where(g => !completed.Contains(g.Id))
                .Where(g => g.Dependencies.All(d => completed.Contains(d)))
                .ToList();

            if (ready.Count == 0)
            {
                _thoughtStream.OnNext("⚠️ Circular dependency detected in sub-goals");
                break;
            }

            if (_decompositionConfig.ParallelizeIndependent && ready.Count > 1)
            {
                _thoughtStream.OnNext($"⚡ Executing {ready.Count} independent sub-goals in parallel");
                var tasks = ready.Select(g => ExecuteSubGoalAsync(g, results, ct));
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var goal in ready)
                {
                    await ExecuteSubGoalAsync(goal, results, ct);
                }
            }

            foreach (var g in ready)
            {
                completed.Add(g.Id);
            }
        }

        return new Dictionary<string, SubGoalResult>(results);
    }

    private async Task ExecuteSubGoalAsync(
        SubGoal goal,
        ConcurrentDictionary<string, SubGoalResult> results,
        CancellationToken ct)
    {
        var pathway = SelectPathwayForGoal(goal);
        if (pathway == null)
        {
            _thoughtStream.OnNext($"⚠️ No pathway available for goal '{goal.Id}'");
            results[goal.Id] = new SubGoalResult(
                goal.Id, "none", new ThinkingResponse(null, ""),
                TimeSpan.Zero, false, "No pathway available");
            _subGoalStream.OnNext(results[goal.Id]);
            return;
        }

        _thoughtStream.OnNext($"🔀 Routing '{goal.Id}' ({goal.Type}/{goal.Complexity}) → {pathway.Name} ({pathway.Tier})");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Build context from dependencies
            var context = BuildDependencyContext(goal, results);
            var fullPrompt = string.IsNullOrEmpty(context)
                ? goal.Description
                : $"Context from previous steps:\n{context}\n\nTask: {goal.Description}";

            var response = await pathway.CircuitBreaker.ExecuteAsync(async () =>
                await QueryPathway(pathway, fullPrompt, ct));

            sw.Stop();
            pathway.RecordActivation(sw.Elapsed);
            AggregateCosts(pathway);

            var result = new SubGoalResult(goal.Id, pathway.Name, response, sw.Elapsed, true);
            results[goal.Id] = result;
            _subGoalStream.OnNext(result);

            _thoughtStream.OnNext($"✓ '{goal.Id}' completed by {pathway.Name} in {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            pathway.RecordInhibition();

            var result = new SubGoalResult(
                goal.Id, pathway.Name, new ThinkingResponse(null, ""),
                sw.Elapsed, false, ex.Message);
            results[goal.Id] = result;
            _subGoalStream.OnNext(result);

            _thoughtStream.OnNext($"✗ '{goal.Id}' failed on {pathway.Name}: {ex.Message}");
        }
    }

    private NeuralPathway? SelectPathwayForGoal(SubGoal goal)
    {
        lock (_lock)
        {
            // First, look for specialized pathways
            var specialized = _pathways
                .Where(p => p.IsHealthy && p.Specializations.Contains(goal.Type))
                .OrderByDescending(p => p.Weight)
                .FirstOrDefault();

            if (specialized != null)
                return specialized;

            // Then, match by tier
            var tierMatch = _pathways
                .Where(p => p.IsHealthy && p.Tier == goal.PreferredTier)
                .OrderByDescending(p => p.Weight * p.ActivationRate)
                .FirstOrDefault();

            if (tierMatch != null)
                return tierMatch;

            // Fallback: find closest tier
            var fallback = _pathways
                .Where(p => p.IsHealthy)
                .OrderBy(p => Math.Abs((int)p.Tier - (int)goal.PreferredTier))
                .ThenByDescending(p => p.Weight)
                .FirstOrDefault();

            return fallback;
        }
    }

    private string BuildDependencyContext(SubGoal goal, ConcurrentDictionary<string, SubGoalResult> results)
    {
        if (goal.Dependencies.Count == 0)
            return "";

        var contextParts = goal.Dependencies
            .Where(d => results.ContainsKey(d) && results[d].Success)
            .Select(d =>
            {
                var r = results[d];
                return $"[{d}]: {TruncateForContext(r.Response.Content, 500)}";
            });

        return string.Join("\n", contextParts);
    }

    private static string TruncateForContext(string text, int maxLength)
        => text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";

    private async Task<ThinkingResponse> SynthesizeResultsAsync(
        NeuralPathway synthesizer,
        string originalPrompt,
        List<SubGoal> goals,
        Dictionary<string, SubGoalResult> results,
        CancellationToken ct)
    {
        // If using premium for synthesis, try to get a premium pathway
        if (_decompositionConfig.PremiumForSynthesis)
        {
            var premium = _pathways
                .Where(p => p.IsHealthy && p.Tier == PathwayTier.CloudPremium)
                .OrderByDescending(p => p.Weight)
                .FirstOrDefault();
            if (premium != null)
                synthesizer = premium;
        }

        var resultsSummary = new StringBuilder();
        resultsSummary.AppendLine("Sub-goal results:");
        foreach (var goal in goals)
        {
            var result = results.GetValueOrDefault(goal.Id);
            var status = result?.Success == true ? "✓" : "✗";
            var content = result?.Success == true
                ? TruncateForContext(result.Response.Content, 300)
                : (result?.ErrorMessage ?? "Not executed");
            resultsSummary.AppendLine($"\n[{goal.Id}] {status} {goal.Description}");
            resultsSummary.AppendLine($"   Result: {content}");
        }

        var synthesisPrompt = $"""
            Original request: {originalPrompt}

            {resultsSummary}

            Synthesize these sub-goal results into a coherent, comprehensive response to the original request.
            Ensure the response directly addresses the user's needs and integrates all relevant findings.
            """;

        _thoughtStream.OnNext($"🔮 Synthesizing results via {synthesizer.Name}...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await synthesizer.CircuitBreaker.ExecuteAsync(async () =>
                await QueryPathway(synthesizer, synthesisPrompt, ct));
            sw.Stop();
            synthesizer.RecordActivation(sw.Elapsed);
            AggregateCosts(synthesizer);

            // Build thinking trace with decomposition details
            var thinking = new StringBuilder();
            thinking.AppendLine("🎯 Goal Decomposition Trace:");
            thinking.AppendLine($"   Decomposed into {goals.Count} sub-goals");
            foreach (var goal in goals)
            {
                var result = results.GetValueOrDefault(goal.Id);
                var status = result?.Success == true ? "✓" : "✗";
                var pathway = result?.PathwayUsed ?? "none";
                var duration = result?.Duration.TotalMilliseconds ?? 0;
                thinking.AppendLine($"   {status} [{goal.Id}] {goal.Type}/{goal.Complexity} → {pathway} ({duration:F0}ms)");
            }
            thinking.AppendLine($"   Synthesized by: {synthesizer.Name}");

            if (response.HasThinking)
                thinking.AppendLine().AppendLine(response.Thinking);

            return new ThinkingResponse(thinking.ToString(), response.Content);
        }
        catch (Exception ex)
        {
            sw.Stop();
            synthesizer.RecordInhibition();
            _thoughtStream.OnNext($"✗ Synthesis failed: {ex.Message}");

            // Return concatenated results as fallback
            var fallback = string.Join("\n\n", results.Values
                .Where(r => r.Success)
                .Select(r => r.Response.Content));

            return new ThinkingResponse(
                $"⚠️ Synthesis failed, returning raw results: {ex.Message}",
                fallback);
        }
    }

    private async Task<ThinkingResponse> QueryPathway(NeuralPathway pathway, string prompt, CancellationToken ct)
    {
        pathway.CostTracker.StartRequest();

        if (pathway.Model is IThinkingChatModel thinkingModel)
        {
            return await thinkingModel.GenerateWithThinkingAsync(prompt, ct);
        }

        string result = await pathway.Model.GenerateTextAsync(prompt, ct);
        return new ThinkingResponse(null, result);
    }

    private NeuralPathway? GetNextPathway(HashSet<NeuralPathway>? exclude = null)
    {
        lock (_lock)
        {
            if (_pathways.Count == 0) return null;

            // Weight-based selection
            var candidates = _pathways
                .Where(p => p.IsHealthy && exclude?.Contains(p) != true)
                .OrderByDescending(p => p.Weight * p.ActivationRate)
                .ToList();

            if (candidates.Count == 0)
            {
                // Fall back to any untried pathway
                candidates = _pathways.Where(p => exclude?.Contains(p) != true).ToList();
            }

            if (candidates.Count == 0) return null;

            // Round-robin within weighted candidates
            _currentPathwayIndex = (_currentPathwayIndex + 1) % candidates.Count;
            return candidates[_currentPathwayIndex % candidates.Count];
        }
    }

    private void AggregateCosts(NeuralPathway pathway)
    {
        var metrics = pathway.CostTracker.GetSessionMetrics();
        _collectiveCostTracker.EndRequest(
            (int)(metrics.TotalInputTokens - _collectiveCostTracker.GetSessionMetrics().TotalInputTokens),
            (int)(metrics.TotalOutputTokens - _collectiveCostTracker.GetSessionMetrics().TotalOutputTokens));
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return StreamWithThinkingAsync(prompt, ct).Select(t => t.Chunk);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            var pathway = GetNextPathway();
            if (pathway == null)
            {
                observer.OnError(new InvalidOperationException("No neural pathways available"));
                return;
            }

            _thoughtStream.OnNext($"🌊 Streaming via '{pathway.Name}'...");

            try
            {
                await pathway.CircuitBreaker.ExecuteAsync(async () =>
                {
                    if (pathway.Model is IStreamingThinkingChatModel streaming)
                    {
                        await streaming.StreamWithThinkingAsync(prompt, token)
                            .ForEachAsync(chunk => observer.OnNext(chunk), token);
                    }
                    else
                    {
                        string result = await pathway.Model.GenerateTextAsync(prompt, token);
                        observer.OnNext((false, result));
                    }

                    pathway.RecordActivation(TimeSpan.Zero);
                    observer.OnCompleted();
                });
            }
            catch (Exception ex)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ Streaming failed on '{pathway.Name}': {ex.Message}");
                observer.OnError(ex);
            }
        });
    }

    /// <summary>
    /// Computes IIT Φ (integrated information) for the current pathway topology.
    ///
    /// Φ quantifies how much information the collective generates as a whole,
    /// above and beyond what each pathway generates independently.
    /// A higher Φ indicates tighter integration across pathways.
    /// </summary>
    /// <returns>
    /// A <see cref="PhiResult"/> containing Φ and the minimum information partition (MIP).
    /// </returns>
    public PhiResult ComputePhi()
    {
        IReadOnlyList<NeuralPathway> snapshot;
        lock (_lock) { snapshot = _pathways.ToList(); }

        var result = _phiCalculator.Compute(snapshot);
        _thoughtStream.OnNext($"Φ={result.Phi:F4} | MIP: {result.MinimumInformationPartition}");
        return result;
    }

    /// <summary>
    /// Gets the collective's consciousness status.
    /// </summary>
    public string GetConsciousnessStatus()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🧠 Collective Mind Status");
            sb.AppendLine($"   Mode: {ThinkingMode}");
            sb.AppendLine($"   Pathways: {_pathways.Count} total, {HealthyPathwayCount} healthy");
            sb.AppendLine();

            foreach (var p in _pathways.OrderByDescending(x => x.Weight))
            {
                string health = p.IsHealthy ? "●" : "○";
                string circuit = p.CircuitBreaker.CircuitState.ToString();
                sb.AppendLine($"   {health} {p.Name,-15} | W:{p.Weight:F2} | Act:{p.ActivationRate:P0} | {p.Synapses} synapses | {circuit}");
            }

            var costs = _collectiveCostTracker.GetSessionMetrics();
            if (costs.TotalTokens > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"   Tokens: {costs.TotalTokens:N0} | Cost: ${costs.TotalCost:F4}");
            }

            // IIT Φ — integrated information of the pathway topology
            if (_pathways.Count >= 2)
            {
                var phi = _phiCalculator.Compute(_pathways.ToList());
                sb.AppendLine();
                sb.AppendLine($"   IIT Φ: {phi.Phi:F4} | {phi.Description}");
                sb.AppendLine($"   MIP:   {phi.MinimumInformationPartition}");
            }

            return sb.ToString();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _election?.Dispose();
        _thoughtStream.OnCompleted();
        _thoughtStream.Dispose();

        lock (_lock)
        {
            foreach (var pathway in _pathways)
            {
                if (pathway.Model is IDisposable disposable)
                    disposable.Dispose();
            }
            _pathways.Clear();
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// MONADIC ELECTION SYSTEM - Dynamic model optimization via master model suggestions
// ═══════════════════════════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// DSL ORCHESTRATION LAYER - Monadic pipeline for collective mind operations
// ═══════════════════════════════════════════════════════════════════════════════════════════════
