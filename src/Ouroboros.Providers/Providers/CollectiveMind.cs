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
public sealed partial class CollectiveMind : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
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
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException ex)
            {
                pathway.RecordInhibition();
                _thoughtStream.OnNext($"✗ Streaming failed on '{pathway.Name}': {ex.Message}");
                observer.OnError(ex);
            }
        });
    }

    /// <summary>
    /// Computes IIT Φ (integrated information) for the current pathway topology.
    /// </summary>
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
