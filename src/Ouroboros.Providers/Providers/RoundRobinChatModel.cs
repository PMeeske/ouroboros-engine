using System.Reactive.Linq;

namespace Ouroboros.Providers;

/// <summary>
/// Round-robin chat model that distributes requests across multiple providers.
/// Supports weighted distribution, automatic failover, and health tracking.
/// </summary>
public sealed class RoundRobinChatModel : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
{
    private readonly List<(Ouroboros.Abstractions.Core.IChatCompletionModel Model, ProviderConfig Config, ProviderStats Stats)> _providers = new();
    private readonly object _lock = new();
    private int _currentIndex;
    private readonly bool _failoverEnabled;
    private readonly int _maxRetries;
    private readonly LlmCostTracker _aggregateCostTracker;

    /// <summary>
    /// Gets the cost tracker that aggregates costs across all providers.
    /// </summary>
    public LlmCostTracker? CostTracker => _aggregateCostTracker;

    /// <summary>
    /// Gets statistics for all providers.
    /// </summary>
    public IReadOnlyList<ProviderStats> ProviderStatistics
    {
        get
        {
            lock (_lock)
            {
                return _providers.Select(p => p.Stats).ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of active (healthy) providers.
    /// </summary>
    public int ActiveProviderCount
    {
        get
        {
            lock (_lock)
            {
                return _providers.Count(p => p.Config.Enabled && p.Stats.IsHealthy);
            }
        }
    }

    /// <summary>
    /// Initializes a new round-robin chat model.
    /// </summary>
    /// <param name="failoverEnabled">If true, automatically retry with next provider on failure.</param>
    /// <param name="maxRetries">Maximum number of providers to try before giving up.</param>
    public RoundRobinChatModel(bool failoverEnabled = true, int maxRetries = 3)
    {
        _failoverEnabled = failoverEnabled;
        _maxRetries = maxRetries;
        _aggregateCostTracker = new LlmCostTracker("round-robin", "Multiple");
    }

    /// <summary>
    /// Adds a provider to the round-robin pool.
    /// </summary>
    public void AddProvider(Ouroboros.Abstractions.Core.IChatCompletionModel model, ProviderConfig config)
    {
        lock (_lock)
        {
            var stats = new ProviderStats { Name = config.Name };
            _providers.Add((model, config, stats));
        }
    }

    /// <summary>
    /// Adds a provider with automatic model creation based on config.
    /// </summary>
    public void AddProvider(ProviderConfig config, ChatRuntimeSettings? settings = null)
    {
        var (endpoint, apiKey, _) = ChatConfig.ResolveWithOverrides(
            config.Endpoint,
            config.ApiKey,
            config.EndpointType.ToString());

        // Use default endpoint if not resolved
        endpoint ??= ChatConfig.GetDefaultEndpoint(config.EndpointType);

        var costTracker = new LlmCostTracker(config.Model ?? "unknown", config.Name);
        Ouroboros.Abstractions.Core.IChatCompletionModel model = CreateModel(config.EndpointType, endpoint ?? "", apiKey ?? "", config.Model ?? "", settings, costTracker);

        AddProvider(model, config);
    }

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

    /// <summary>
    /// Gets the next provider using weighted round-robin selection.
    /// Returns the provider tuple and its index within the providers list.
    /// </summary>
    private (Ouroboros.Abstractions.Core.IChatCompletionModel Model, ProviderConfig Config, ProviderStats Stats, int Index)? GetNextProvider(HashSet<int>? excludeIndices = null)
    {
        lock (_lock)
        {
            if (_providers.Count == 0) return null;

            int startIndex = _currentIndex;
            int attempts = 0;

            while (attempts < _providers.Count)
            {
                int idx = _currentIndex;
                var (model, config, stats) = _providers[idx];
                attempts++;

                // Check exclusion BEFORE using the provider
                if (!config.Enabled || (!stats.IsHealthy && _failoverEnabled) || excludeIndices?.Contains(idx) == true)
                {
                    _currentIndex = (_currentIndex + 1) % _providers.Count;
                    continue;
                }

                // Advance pointer AFTER selecting a valid provider
                _currentIndex = (_currentIndex + 1) % _providers.Count;
                return (model, config, stats, idx);
            }

            // If failover is disabled or all providers are unhealthy, try any enabled provider
            for (int i = 0; i < _providers.Count; i++)
            {
                var provider = _providers[i];
                if (provider.Config.Enabled && excludeIndices?.Contains(i) != true)
                    return (provider.Model, provider.Config, provider.Stats, i);
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GenerateWithThinkingAsync(prompt, ct);
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        HashSet<int> triedIndices = new();
        Exception? lastException = null;
        int retries = 0;

        while (retries < _maxRetries)
        {
            var provider = GetNextProvider(triedIndices);
            if (provider == null)
            {
                throw new InvalidOperationException("No available providers in round-robin pool");
            }

            var (model, config, stats, providerIndex) = provider.Value;
            triedIndices.Add(providerIndex);

            try
            {
                Interlocked.Increment(ref stats.TotalRequests);
                _aggregateCostTracker.StartRequest();

                ThinkingResponse result;
                if (model is IThinkingChatModel thinkingModel)
                {
                    result = await thinkingModel.GenerateWithThinkingAsync(prompt, ct);
                }
                else
                {
                    string text = await model.GenerateTextAsync(prompt, ct);
                    result = new ThinkingResponse(null, text);
                }

                // Check for fallback responses (indicates failure)
                if (result.Content.Contains("-fallback:"))
                {
                    throw new InvalidOperationException($"Provider {config.Name} returned fallback response");
                }

                // Success
                Interlocked.Increment(ref stats.SuccessfulRequests);
                Interlocked.Exchange(ref stats.ConsecutiveFailures, 0);
                stats.LastSuccess = DateTime.UtcNow;

                // Track costs from provider's tracker
                if (model is ICostAwareChatModel costAware && costAware.CostTracker != null)
                {
                    var metrics = costAware.CostTracker.GetSessionMetrics();
                    _aggregateCostTracker.EndRequest((int)metrics.TotalInputTokens, (int)metrics.TotalOutputTokens);
                }
                else
                {
                    _aggregateCostTracker.EndRequest(0, 0);
                }

                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // deliberate cancellation — don't retry
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Interlocked.Increment(ref stats.FailedRequests);
                Interlocked.Increment(ref stats.ConsecutiveFailures);
                stats.LastFailure = DateTime.UtcNow;
                lastException = ex;

                System.Diagnostics.Trace.TraceWarning("[RoundRobinChatModel] Provider '{0}' failed: {1}", config.Name, ex.Message);

                if (!_failoverEnabled)
                    throw;

                retries++;
            }
        }

        throw new InvalidOperationException($"All {retries} providers failed", lastException!);
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
            HashSet<int> triedIndices = new();
            int retries = 0;

            while (retries < _maxRetries)
            {
                var provider = GetNextProvider(triedIndices);
                if (provider == null)
                {
                    observer.OnError(new InvalidOperationException("No available providers"));
                    return;
                }

                var (model, config, stats, providerIndex) = provider.Value;
                triedIndices.Add(providerIndex);

                try
                {
                    Interlocked.Increment(ref stats.TotalRequests);

                    if (model is IStreamingThinkingChatModel streamingThinking)
                    {
                        bool hasContent = false;
                        await streamingThinking.StreamWithThinkingAsync(prompt, token)
                            .ForEachAsync(chunk =>
                            {
                                hasContent = true;
                                observer.OnNext(chunk);
                            }, token);

                        if (hasContent)
                        {
                            Interlocked.Increment(ref stats.SuccessfulRequests);
                            Interlocked.Exchange(ref stats.ConsecutiveFailures, 0);
                            stats.LastSuccess = DateTime.UtcNow;
                            observer.OnCompleted();
                            return;
                        }
                    }
                    else
                    {
                        // Fall back to non-streaming
                        string result = await model.GenerateTextAsync(prompt, token);
                        if (!result.Contains("-fallback:"))
                        {
                            Interlocked.Increment(ref stats.SuccessfulRequests);
                            Interlocked.Exchange(ref stats.ConsecutiveFailures, 0);
                            stats.LastSuccess = DateTime.UtcNow;
                            observer.OnNext((false, result));
                            observer.OnCompleted();
                            return;
                        }
                    }

                    throw new InvalidOperationException($"Provider {config.Name} returned empty or fallback response");
                }
                catch (OperationCanceledException ex) when (token.IsCancellationRequested)
                {
                    observer.OnError(ex); // deliberate cancellation — signal error, don't retry
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref stats.FailedRequests);
                    Interlocked.Increment(ref stats.ConsecutiveFailures);
                    stats.LastFailure = DateTime.UtcNow;

                    System.Diagnostics.Trace.TraceWarning("[RoundRobinChatModel] Provider '{0}' streaming failed: {1}", config.Name, ex.Message);

                    if (!_failoverEnabled)
                    {
                        observer.OnError(ex);
                        return;
                    }

                    retries++;
                }
            }

            observer.OnError(new InvalidOperationException($"All {retries} providers failed"));
        });
    }

    /// <summary>
    /// Resets the health status of all providers.
    /// </summary>
    public void ResetHealth()
    {
        lock (_lock)
        {
            foreach (var (_, _, stats) in _providers)
            {
                Interlocked.Exchange(ref stats.ConsecutiveFailures, 0);
            }
        }
    }

    /// <summary>
    /// Gets a formatted status summary of all providers.
    /// </summary>
    public string GetStatusSummary()
    {
        lock (_lock)
        {
            if (_providers.Count == 0)
                return "No providers configured";

            var activeCount = _providers.Count(p => p.Config.Enabled && p.Stats.IsHealthy);
            var lines = new List<string>
            {
                $"Round-Robin Pool: {_providers.Count} providers ({activeCount} healthy)",
                ""
            };

            foreach (var (_, config, stats) in _providers)
            {
                string status = stats.IsHealthy ? "✓" : "✗";
                string enabled = config.Enabled ? "" : " [disabled]";
                lines.Add($"  {status} {config.Name}{enabled}: {stats.SuccessfulRequests}/{stats.TotalRequests} ({stats.SuccessRate:P0})");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var (model, _, _) in _providers)
            {
                if (model is IDisposable disposable)
                    disposable.Dispose();
            }
            _providers.Clear();
        }
    }
}