// <copyright file="HyperonMeTTaGrpcClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Ouroboros.Pipeline.MeTTa.Grpc;

namespace Ouroboros.Pipeline.MeTTa;

/// <summary>
/// gRPC client for the Hyperon MeTTa sidecar. Routes advanced symbolic queries
/// (complex pattern matching, PLN inference, cross-domain reasoning) to a full
/// Hyperon/MeTTa engine running in a sidecar process or container.
/// </summary>
/// <remarks>
/// Falls back gracefully when the sidecar is unavailable. All methods are async
/// and cancellation-aware.
/// </remarks>
public sealed class HyperonMeTTaGrpcClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly HyperonMeTTaService.HyperonMeTTaServiceClient _client;
    private readonly ILogger<HyperonMeTTaGrpcClient>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonMeTTaGrpcClient"/> class.
    /// </summary>
    /// <param name="endpoint">The gRPC endpoint (e.g. "http://localhost:50052").</param>
    /// <param name="logger">Optional logger.</param>
    public HyperonMeTTaGrpcClient(string endpoint, ILogger<HyperonMeTTaGrpcClient>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        _logger = logger;
        _channel = GrpcChannel.ForAddress(endpoint);
        _client = new HyperonMeTTaService.HyperonMeTTaServiceClient(_channel);
    }

    /// <summary>
    /// Initializes a new instance with an existing gRPC channel.
    /// </summary>
    /// <param name="channel">The gRPC channel.</param>
    /// <param name="logger">Optional logger.</param>
    public HyperonMeTTaGrpcClient(GrpcChannel channel, ILogger<HyperonMeTTaGrpcClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _logger = logger;
        _channel = channel;
        _client = new HyperonMeTTaService.HyperonMeTTaServiceClient(_channel);
    }

    /// <summary>
    /// Executes a MeTTa query on the remote Hyperon engine.
    /// </summary>
    public async Task<(bool Success, IReadOnlyList<string> Results, string? Error)> ExecuteQueryAsync(
        string query,
        IReadOnlyList<string>? contextAtoms = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var request = new ExecuteQueryRequest { Query = query };
        if (contextAtoms is not null)
            request.ContextAtoms.AddRange(contextAtoms);

        try
        {
            var response = await _client.ExecuteQueryAsync(request, cancellationToken: ct).ConfigureAwait(false);
            return (response.Success, response.Results, response.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC ExecuteQuery failed");
            return (false, [], $"gRPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a fact to the remote AtomSpace.
    /// </summary>
    public async Task<(bool Success, string? Error)> AddFactAsync(string fact, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fact);

        try
        {
            var response = await _client.AddFactAsync(new AddFactRequest { Fact = fact }, cancellationToken: ct).ConfigureAwait(false);
            return (response.Success, response.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC AddFact failed");
            return (false, $"gRPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a rule on the remote engine.
    /// </summary>
    public async Task<(bool Success, string? Result, string? Error)> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);

        try
        {
            var response = await _client.ApplyRuleAsync(new ApplyRuleRequest { Rule = rule }, cancellationToken: ct).ConfigureAwait(false);
            return (response.Success, response.Result, response.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC ApplyRule failed");
            return (false, null, $"gRPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies a plan using the remote symbolic reasoner.
    /// </summary>
    public async Task<(bool Valid, string? Explanation, string? Error)> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plan);

        try
        {
            var response = await _client.VerifyPlanAsync(new VerifyPlanRequest { Plan = plan }, cancellationToken: ct).ConfigureAwait(false);
            return (response.Valid, response.Explanation, response.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC VerifyPlan failed");
            return (false, null, $"gRPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the remote engine state.
    /// </summary>
    public async Task<bool> ResetAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.ResetAsync(new ResetRequest(), cancellationToken: ct).ConfigureAwait(false);
            return response.Success;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC Reset failed");
            return false;
        }
    }

    /// <summary>
    /// Performs complex pattern matching on the remote engine.
    /// </summary>
    public async Task<(bool Success, IReadOnlyList<string> Bindings, string? Error)> PatternMatchAsync(
        string pattern,
        string against,
        int limit = 0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(against);

        var request = new PatternMatchRequest { Pattern = pattern, Against = against };
        if (limit > 0)
            request.Limit = limit;

        try
        {
            var response = await _client.PatternMatchAsync(request, cancellationToken: ct).ConfigureAwait(false);
            return (response.Success, response.Bindings, response.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC PatternMatch failed");
            return (false, [], $"gRPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs PLN inference on the remote engine.
    /// </summary>
    public async Task<(bool Success, string? Conclusion, double Confidence, string? Error)> PlnInferAsync(
        string premise,
        string rule,
        double minConfidence = 0.0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(premise);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);

        var request = new PlnInferRequest
        {
            Premise = premise,
            Rule = rule,
            MinConfidence = minConfidence,
        };

        try
        {
            var response = await _client.PlnInferAsync(request, cancellationToken: ct).ConfigureAwait(false);
            return (response.Success, response.Conclusion, response.Confidence, response.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Hyperon gRPC PlnInfer failed");
            return (false, null, 0.0, $"gRPC error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks whether the Hyperon sidecar is reachable and healthy.
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.HealthCheckAsync(new HealthCheckRequest(), cancellationToken: ct).ConfigureAwait(false);
            return response.Healthy;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogDebug(ex, "Hyperon gRPC health check failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _channel.Dispose();
}
