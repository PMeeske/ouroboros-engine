// <copyright file="LoadBalancedChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers.LoadBalancing;

/// <summary>
/// Chat model wrapper that provides load balancing across multiple provider instances.
/// Automatically handles rate limiting (429) by rotating to healthy providers.
/// Uses Polly for resilient retry logic with exponential backoff.
/// Implements IChatCompletionModel for seamless integration with existing code.
/// </summary>
public sealed class LoadBalancedChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel, IDisposable
{
    private readonly IProviderLoadBalancer<Ouroboros.Abstractions.Core.IChatCompletionModel> _loadBalancer;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadBalancedChatModel"/> class.
    /// </summary>
    /// <param name="strategy">The load balancing strategy to use. If null, defaults to AdaptiveHealth.</param>
    public LoadBalancedChatModel(IProviderSelectionStrategy? strategy = null, ILogger<LoadBalancedChatModel>? logger = null)
    {
        _logger = logger ?? NullLogger<LoadBalancedChatModel>.Instance;
        _loadBalancer = new ProviderLoadBalancer<Ouroboros.Abstractions.Core.IChatCompletionModel>(strategy);
        _retryPolicy = CreateRetryPolicy();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadBalancedChatModel"/> class with a custom load balancer.
    /// </summary>
    /// <param name="loadBalancer">Custom load balancer instance.</param>
    public LoadBalancedChatModel(IProviderLoadBalancer<Ouroboros.Abstractions.Core.IChatCompletionModel> loadBalancer)
    {
        ArgumentNullException.ThrowIfNull(loadBalancer);
        _loadBalancer = loadBalancer;
        _retryPolicy = CreateRetryPolicy();
    }

    /// <summary>
    /// Creates Polly retry policy for handling provider failures and selection retries.
    /// </summary>
    private AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<HttpRequestException>(ex => IsRateLimitError(ex))
            .Or<InvalidOperationException>(ex => ex.Message.Contains("No healthy providers"))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogInformation("Retry {RetryCount} after {DelaySeconds}s due to {ExceptionType}: {Message}", retryCount, timespan.TotalSeconds, exception.GetType().Name, exception.Message);
                });
    }

    /// <summary>
    /// Registers a provider with the load balancer.
    /// </summary>
    /// <param name="providerId">Unique identifier for the provider.</param>
    /// <param name="model">The chat model instance.</param>
    public void RegisterProvider(string providerId, Ouroboros.Abstractions.Core.IChatCompletionModel model)
    {
        if (_loadBalancer is ProviderLoadBalancer<Ouroboros.Abstractions.Core.IChatCompletionModel> balancer)
        {
            balancer.RegisterProvider(providerId, model);
        }
        else
        {
            throw new InvalidOperationException("Cannot register provider with custom load balancer");
        }
    }

    /// <summary>
    /// Gets the current health status of all providers.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyDictionary<string, ProviderHealthStatus> GetHealthStatus()
    {
        return _loadBalancer.GetHealthStatus();
    }

    /// <summary>
    /// Gets the load balancing strategy being used.
    /// </summary>
    public IProviderSelectionStrategy Strategy => _loadBalancer.Strategy;

    /// <summary>
    /// Gets the total number of registered providers.
    /// </summary>
    public int ProviderCount => _loadBalancer.ProviderCount;

    /// <summary>
    /// Gets the number of currently healthy providers.
    /// </summary>
    public int HealthyProviderCount => _loadBalancer.HealthyProviderCount;

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        List<string> attemptedProviders = new();

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                // Select a provider
                Result<ProviderSelectionResult<Ouroboros.Abstractions.Core.IChatCompletionModel>, string> selectionResult =
                    await _loadBalancer.SelectProviderAsync().ConfigureAwait(false);

                if (selectionResult.IsFailure)
                {
                    // No healthy providers available
                    string error = selectionResult.Match(_ => string.Empty, err => err);
                    _logger.LogWarning("Provider selection failed: {Error}", error);

                    // Throw to trigger Polly retry
                    throw new InvalidOperationException($"No healthy providers available: {error}");
                }

                ProviderSelectionResult<Ouroboros.Abstractions.Core.IChatCompletionModel> selection = selectionResult.Match(
                    result => result,
                    _ => throw new InvalidOperationException("Should not reach here"));

                attemptedProviders.Add(selection.ProviderId);
                Stopwatch sw = Stopwatch.StartNew();

                try
                {
                    string result = await selection.Provider.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
                    sw.Stop();

                    // Record successful execution
                    _loadBalancer.RecordExecution(
                        selection.ProviderId,
                        sw.Elapsed.TotalMilliseconds,
                        success: true,
                        wasRateLimited: false);

                    _logger.LogInformation("Success with provider '{ProviderId}' in {LatencyMs:F0}ms", selection.ProviderId, sw.Elapsed.TotalMilliseconds);

                    return result;
                }
                catch (HttpRequestException ex) when (IsRateLimitError(ex))
                {
                    sw.Stop();

                    // Record rate limited execution
                    _loadBalancer.RecordExecution(
                        selection.ProviderId,
                        sw.Elapsed.TotalMilliseconds,
                        success: false,
                        wasRateLimited: true);

                    _logger.LogWarning("Provider '{ProviderId}' rate limited (429). Attempting with another provider...", selection.ProviderId);

                    // Re-throw to trigger Polly retry with exponential backoff
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    sw.Stop();

                    // Record failed execution
                    _loadBalancer.RecordExecution(
                        selection.ProviderId,
                        sw.Elapsed.TotalMilliseconds,
                        success: false,
                        wasRateLimited: false);

                    _logger.LogWarning(ex, "Provider '{ProviderId}' failed", selection.ProviderId);

                    // Re-throw to trigger Polly retry
                    throw;
                }
            }).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // All retries exhausted - return error message for graceful degradation
            _logger.LogWarning(ex, "All retries exhausted");

            string providersAttempted = attemptedProviders.Count > 0
                ? $"Attempted: {string.Join(", ", attemptedProviders)}"
                : "No providers could be selected";

            return $"[load-balanced-error] All providers exhausted. {providersAttempted}. Error: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "All retries exhausted");

            string providersAttempted = attemptedProviders.Count > 0
                ? $"Attempted: {string.Join(", ", attemptedProviders)}"
                : "No providers could be selected";

            return $"[load-balanced-error] All providers exhausted. {providersAttempted}. Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if an exception indicates a rate limit error (HTTP 429).
    /// </summary>
    private static bool IsRateLimitError(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            // Check for 429 status code in various ways
            string message = httpEx.Message;
            if (message.Contains("429") || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check StatusCode property if available (requires .NET 5+)
            if (httpEx.StatusCode.HasValue && httpEx.StatusCode.Value == HttpStatusCode.TooManyRequests)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Load balancer doesn't own the providers, so no disposal needed
        _disposed = true;
    }
}
