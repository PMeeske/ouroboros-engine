#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.Providers;

public sealed partial class CollectiveMind
{
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

    private static SubGoalComplexity ParseComplexity(string? s) => s?.ToLowerInvariant() switch
    {
        "trivial" => SubGoalComplexity.Trivial,
        "simple" => SubGoalComplexity.Simple,
        "moderate" => SubGoalComplexity.Moderate,
        "complex" => SubGoalComplexity.Complex,
        "expert" => SubGoalComplexity.Expert,
        _ => SubGoalComplexity.Moderate
    };

    private static SubGoalType ParseGoalType(string? s) => s?.ToLowerInvariant() switch
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

    private static SubGoalType EstimateGoalType(string text)
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
        if (_decompositionConfig.TypeRouting.TryGetValue(type, out var typeTier))
        {
            if (complexity <= SubGoalComplexity.Simple && _decompositionConfig.PreferLocalForSimple)
                return PathwayTier.Local;
            return typeTier;
        }

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

        while (completed.Count < goals.Count)
        {
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
            var specialized = _pathways
                .Where(p => p.IsHealthy && p.Specializations.Contains(goal.Type))
                .OrderByDescending(p => p.Weight)
                .FirstOrDefault();

            if (specialized != null)
                return specialized;

            var tierMatch = _pathways
                .Where(p => p.IsHealthy && p.Tier == goal.PreferredTier)
                .OrderByDescending(p => p.Weight * p.ActivationRate)
                .FirstOrDefault();

            if (tierMatch != null)
                return tierMatch;

            return _pathways
                .Where(p => p.IsHealthy)
                .OrderBy(p => Math.Abs((int)p.Tier - (int)goal.PreferredTier))
                .ThenByDescending(p => p.Weight)
                .FirstOrDefault();
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

            var fallback = string.Join("\n\n", results.Values
                .Where(r => r.Success)
                .Select(r => r.Response.Content));

            return new ThinkingResponse(
                $"⚠️ Synthesis failed, returning raw results: {ex.Message}",
                fallback);
        }
    }
}
