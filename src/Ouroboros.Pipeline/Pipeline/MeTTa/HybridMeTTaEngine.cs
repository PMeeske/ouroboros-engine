// <copyright file="HybridMeTTaEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Pipeline.MeTTa;

/// <summary>
/// Hybrid MeTTa engine that routes queries between a fast local engine and an
/// advanced remote Hyperon engine via gRPC. Simple queries (direct facts, simple
/// rules, plan verification) stay local. Advanced queries (complex pattern matching,
/// PLN inference, cross-domain reasoning) are routed to the Hyperon sidecar when
/// healthy; otherwise they fall back to the local engine.
/// </summary>
public sealed class HybridMeTTaEngine : IMeTTaEngine
{
    private readonly IMeTTaEngine _local;
    private readonly HyperonMeTTaGrpcClient? _remote;
    private readonly HybridRoutingPolicy _policy;
    private bool _disposed;

    /// <summary>
    /// Creates a hybrid engine with the given local engine and optional remote client.
    /// </summary>
    /// <param name="local">The local MeTTa engine (required).</param>
    /// <param name="remote">Optional gRPC client for the Hyperon sidecar.</param>
    /// <param name="policy">Routing policy (default: automatic).</param>
    public HybridMeTTaEngine(
        IMeTTaEngine local,
        HyperonMeTTaGrpcClient? remote = null,
        HybridRoutingPolicy? policy = null)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
        _remote = remote;
        _policy = policy ?? HybridRoutingPolicy.Default;
    }

    /// <summary>
    /// Whether the remote Hyperon sidecar was healthy on the last check.
    /// </summary>
    public bool IsRemoteAvailable => _remote is not null && _policy.LastHealthCheck;

    /// <inheritdoc/>
    public async Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_policy.ShouldRouteRemote(query, QueryKind.ExecuteQuery) && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            var (success, results, error) = await _remote!.ExecuteQueryAsync(query, null, ct).ConfigureAwait(false);
            if (success && results.Count > 0)
                return Result<string, string>.Success(string.Join("\n", results));
            if (!string.IsNullOrEmpty(error))
                return Result<string, string>.Failure($"Remote: {error}");
        }

        return await _local.ExecuteQueryAsync(query, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Facts are always added to both engines if remote is available.
        var localResult = await _local.AddFactAsync(fact, ct).ConfigureAwait(false);

        if (_remote is not null && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            var (success, error) = await _remote.AddFactAsync(fact, ct).ConfigureAwait(false);
            if (!success)
            {
                // Local succeeded but remote failed — log and continue.
                // The fact is at least in the local engine.
                return Result<Unit, string>.Failure($"Local OK, remote failed: {error}");
            }
        }

        return localResult;
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_policy.ShouldRouteRemote(rule, QueryKind.ApplyRule) && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            var (success, result, error) = await _remote!.ApplyRuleAsync(rule, ct).ConfigureAwait(false);
            if (success && !string.IsNullOrEmpty(result))
                return Result<string, string>.Success(result);
            if (!string.IsNullOrEmpty(error))
                return Result<string, string>.Failure($"Remote: {error}");
        }

        return await _local.ApplyRuleAsync(rule, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_policy.ShouldRouteRemote(plan, QueryKind.VerifyPlan) && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            var (valid, explanation, error) = await _remote!.VerifyPlanAsync(plan, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(error))
                return Result<bool, string>.Failure($"Remote: {error}");
            return Result<bool, string>.Success(valid);
        }

        return await _local.VerifyPlanAsync(plan, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var localResult = await _local.ResetAsync(ct).ConfigureAwait(false);

        if (_remote is not null && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            await _remote.ResetAsync(ct).ConfigureAwait(false);
        }

        return localResult;
    }

    /// <summary>
    /// Performs complex pattern matching, routing to the remote engine when available.
    /// This is an extension beyond <see cref="IMeTTaEngine"/> for advanced use cases.
    /// </summary>
    public async Task<Result<IReadOnlyList<string>, string>> PatternMatchAsync(
        string pattern,
        string against,
        int limit = 0,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_remote is not null && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            var (success, bindings, error) = await _remote.PatternMatchAsync(pattern, against, limit, ct).ConfigureAwait(false);
            if (success)
                return Result<IReadOnlyList<string>, string>.Success(bindings);
            return Result<IReadOnlyList<string>, string>.Failure($"Remote: {error}");
        }

        return Result<IReadOnlyList<string>, string>.Failure("PatternMatch requires remote Hyperon sidecar");
    }

    /// <summary>
    /// Performs PLN inference, routing to the remote engine when available.
    /// This is an extension beyond <see cref="IMeTTaEngine"/> for advanced use cases.
    /// </summary>
    public async Task<Result<(string Conclusion, double Confidence), string>> PlnInferAsync(
        string premise,
        string rule,
        double minConfidence = 0.0,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_remote is not null && await IsRemoteHealthyAsync(ct).ConfigureAwait(false))
        {
            var (success, conclusion, confidence, error) = await _remote.PlnInferAsync(premise, rule, minConfidence, ct).ConfigureAwait(false);
            if (success && !string.IsNullOrEmpty(conclusion))
                return Result<(string, double), string>.Success((conclusion, confidence));
            return Result<(string, double), string>.Failure($"Remote: {error}");
        }

        return Result<(string, double), string>.Failure("PLN inference requires remote Hyperon sidecar");
    }

    private async ValueTask<bool> IsRemoteHealthyAsync(CancellationToken ct)
    {
        if (_remote is null)
            return false;

        // Cache health check result for a short window to avoid hammering the sidecar.
        if (_policy.IsHealthCheckFresh)
            return _policy.LastHealthCheck;

        var healthy = await _remote.IsHealthyAsync(ct).ConfigureAwait(false);
        _policy.RecordHealthCheck(healthy);
        return healthy;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _local.Dispose();
        _remote?.Dispose();
    }
}

/// <summary>
/// Routing policy that decides whether a query should be executed locally or
/// sent to the remote Hyperon sidecar.
/// </summary>
public sealed class HybridRoutingPolicy
{
    /// <summary>Default policy: route advanced queries to remote when healthy.</summary>
    public static HybridRoutingPolicy Default { get; } = new();

    /// <summary>
    /// How long to cache the health check result before re-checking.
    /// </summary>
    public TimeSpan HealthCheckCacheDuration { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum query complexity (character length) for local execution.
    /// Queries longer than this are considered "advanced" and routed remote.
    /// </summary>
    public int LocalComplexityThreshold { get; init; } = 500;

    /// <summary>
    /// Keywords that force remote routing when present in a query.
    /// </summary>
    public IReadOnlySet<string> RemoteKeywords { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PlnInfer", "PatternMatch", "Distinction", "Grounding", "CrossDomain",
        "!", "match", "let", "case", "superpose", "collapse",
    };

    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;

    /// <summary>
    /// Whether the cached health check is still fresh.
    /// </summary>
    public bool IsHealthCheckFresh => (DateTimeOffset.UtcNow - _lastCheck) < HealthCheckCacheDuration;

    /// <summary>
    /// Result of the last health check.
    /// </summary>
    public bool LastHealthCheck { get; private set; }

    /// <summary>
    /// Records a health check result and resets the cache timer.
    /// </summary>
    public void RecordHealthCheck(bool healthy)
    {
        LastHealthCheck = healthy;
        _lastCheck = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Determines whether a query should be routed to the remote engine.
    /// </summary>
    public bool ShouldRouteRemote(string query, QueryKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        // PatternMatch and PlnInfer always go remote when available.
        if (kind is QueryKind.PatternMatch or QueryKind.PlnInfer)
            return true;

        // Check complexity threshold.
        if (query.Length > LocalComplexityThreshold)
            return true;

        // Check for remote-only keywords.
        foreach (var keyword in RemoteKeywords)
        {
            if (query.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Categories of MeTTa queries for routing decisions.
/// </summary>
public enum QueryKind
{
    ExecuteQuery,
    AddFact,
    ApplyRule,
    VerifyPlan,
    PatternMatch,
    PlnInfer,
}
