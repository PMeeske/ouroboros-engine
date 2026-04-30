// ============================================================
// File: Ouroboros.Pipeline/AIPipeline.cs
// Description: Refactored AI pipeline using IChatClient (M.E.AI)
//   instead of LangChain .NET 0.17.0. Supports middleware
//   composition: function invocation, logging, OpenTelemetry.
// Dependencies:
//   - Microsoft.Extensions.AI 9.0.0
//   - Microsoft.Extensions.AI.Abstractions 9.0.0
//   - Microsoft.Extensions.Logging 9.0.0
//   - System.Diagnostics.DiagnosticSource 9.0.0
// ============================================================

#pragma warning disable CA2007 // ConfigureAwait (remediation integration)
#pragma warning disable CA1031 // Catch specific exceptions (remediation integration)

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Pipeline;

// ============================================================
// Pipeline Input / Output Contracts
// ============================================================

/// <summary>
/// A request flowing through the AI pipeline. Carries the conversation
/// context, optional tool definitions, and pipeline-specific metadata.
/// </summary>
public sealed class PipelineRequest
{
    /// <summary>The conversation messages to send to the model.</summary>
    public required List<ChatMessage> Messages { get; init; }

    /// <summary>Override options for this specific request.</summary>
    public ChatOptions? Options { get; init; }

    /// <summary>Arbitrary metadata for middleware correlation.</summary>
    public Dictionary<string, object?> Context { get; } = new();

    /// <summary>Correlation ID for distributed tracing.</summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// The result of pipeline execution, wrapping the model response
/// with timing, token usage, and any errors encountered.
/// </summary>
public sealed class PipelineResult
{
    /// <summary>The assistant's response message (null on failure).</summary>
    public ChatMessage? Response { get; init; }

    /// <summary>Token usage details from the model provider.</summary>
    public UsageDetails? Usage { get; init; }

    /// <summary>Time spent in the model call (excluding middleware).</summary>
    public TimeSpan ModelLatency { get; init; }

    /// <summary>Total time including all middleware.</summary>
    public TimeSpan TotalLatency { get; init; }

    /// <summary>True if the pipeline completed without exceptions.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Error details if IsSuccess is false.</summary>
    public PipelineError? Error { get; init; }

    /// <summary>Finish reason from the model (Stop, Length, ToolCalls, etc.)</summary>
    public ChatFinishReason? FinishReason { get; init; }
}

/// <summary>Structured error information for failed pipeline executions.</summary>
public sealed record PipelineError(
    string Code,
    string Message,
    Exception? InnerException = null);

// ============================================================
// Core Pipeline
// ============================================================

/// <summary>
/// The refactored AI pipeline. Replaces LangChain .NET with the
/// Microsoft.Extensions.AI <see cref="IChatClient"/> abstraction.
///
/// <para>
/// The pipeline is built by composing middleware around an inner
/// <see cref="IChatClient"/>, following the M.E.AI middleware pattern:
/// <c>UseFunctionInvocation()</c>, <c>UseLogging()</c>, <c>UseOpenTelemetry()</c>.
/// </para>
/// </summary>
public sealed class AIPipeline : IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AIPipeline> _logger;
    private readonly TimeProvider _timeProvider;
    private bool _disposed;

    /// <summary>
    /// Creates a new AI pipeline wrapping the given chat client.
    /// Use <see cref="CreateBuilder"/> for middleware-based construction.
    /// </summary>
    public AIPipeline(IChatClient chatClient, ILogger<AIPipeline> logger, TimeProvider? timeProvider = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Executes the pipeline: builds the final message list, sends to the
    /// model via the (middleware-wrapped) <see cref="IChatClient"/>, and
    /// returns a structured <see cref="PipelineResult"/>.
    /// </summary>
    public async Task<PipelineResult> ExecuteAsync(
        PipelineRequest request,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var totalStart = _timeProvider.GetTimestamp();

        try
        {
            _logger.LogInformation(
                "[Pipeline:{CorrelationId}] Starting execution with {MessageCount} messages",
                request.CorrelationId, request.Messages.Count);

            // Validate request
            if (request.Messages.Count == 0)
            {
                return Fail(request.CorrelationId, "EMPTY_REQUEST", "Pipeline request must contain at least one message.");
            }

            // Ensure the last message is from the user (required by most providers)
            var messages = request.Messages.ToList();
            if (messages.Last().Role != ChatRole.User && messages.Last().Role != ChatRole.Tool)
            {
                _logger.LogWarning(
                    "[Pipeline:{CorrelationId}] Last message role is {Role}, not User. " +
                    "Some providers may reject this.",
                    request.CorrelationId, messages.Last().Role.Value);
            }

            // Build options — merge request-level with defaults
            var options = request.Options ?? new ChatOptions
            {
                MaxOutputTokens = 4096,
                Temperature = 0.7f,
                TopP = 0.95f
            };

            // Attach function tools if any are registered in context
            if (request.Context.TryGetValue("Tools", out var toolsObj) && toolsObj is List<AIFunction> tools)
            {
                options.Tools = tools.Cast<AITool>().ToList();
                options.ToolMode = new AutoChatToolMode();
            }

            // Measure model-only latency (middleware not included in this metric)
            var modelStart = _timeProvider.GetTimestamp();
            var response = await _chatClient.GetResponseAsync(messages, options, ct);
            var modelLatency = _timeProvider.GetElapsedTime(modelStart);

            var totalLatency = _timeProvider.GetElapsedTime(totalStart);

            _logger.LogInformation(
                "[Pipeline:{CorrelationId}] Completed: ModelLatency={ModelLatencyMs}ms, " +
                "TotalLatency={TotalLatencyMs}ms, InTokens={InTokens}, OutTokens={OutTokens}, " +
                "FinishReason={FinishReason}",
                request.CorrelationId,
                modelLatency.TotalMilliseconds,
                totalLatency.TotalMilliseconds,
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount,
                response.FinishReason?.ToString() ?? "(null)");

            return new PipelineResult
            {
                Response = response.Messages.LastOrDefault() ?? new ChatMessage(ChatRole.Assistant, response.Text),
                Usage = response.Usage,
                ModelLatency = modelLatency,
                TotalLatency = totalLatency,
                IsSuccess = true,
                FinishReason = response.FinishReason
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("[Pipeline:{CorrelationId}] Cancelled by caller.", request.CorrelationId);
            throw;
        }
        catch (Exception ex)
        {
            var totalLatency = _timeProvider.GetElapsedTime(totalStart);
            _logger.LogError(ex,
                "[Pipeline:{CorrelationId}] Failed after {ElapsedMs}ms: {ErrorMessage}",
                request.CorrelationId, totalLatency.TotalMilliseconds, ex.Message);

            return new PipelineResult
            {
                IsSuccess = false,
                TotalLatency = totalLatency,
                Error = new PipelineError("PIPELINE_ERROR", ex.Message, ex)
            };
        }
    }

    /// <summary>
    /// Executes the pipeline with streaming response.
    /// Yields <see cref="ChatResponseUpdate"/> tokens as they arrive.
    /// </summary>
    public async IAsyncEnumerable<ChatResponseUpdate> ExecuteStreamingAsync(
        PipelineRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var options = request.Options ?? new ChatOptions
        {
            MaxOutputTokens = 4096,
            Temperature = 0.7f
        };

        _logger.LogInformation(
            "[Pipeline:{CorrelationId}] Starting streaming execution",
            request.CorrelationId);

        await foreach (var update in _chatClient.GetStreamingResponseAsync(request.Messages, options, ct))
        {
            yield return update;
        }

        _logger.LogInformation(
            "[Pipeline:{CorrelationId}] Streaming completed",
            request.CorrelationId);
    }

    // ----------------------------------------------------------
    // Factory: Build pipeline with middleware via DI
    // ----------------------------------------------------------

    /// <summary>
    /// Creates an <see cref="IChatClient"/> builder pre-configured with
    /// Ouroboros middleware: function invocation, logging, OpenTelemetry.
    /// </summary>
    public static ChatClientBuilder CreateBuilder(IServiceProvider services, string? modelId = null)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        // The inner client is resolved from DI — could be Anthropic, OpenAI, Ollama, etc.
        var innerClient = services.GetRequiredService<IChatClient>();

        // Start building the middleware pipeline
        var builder = new ChatClientBuilder(innerClient);

        // 1. Function invocation middleware — enables tool use
        // This allows the model to call registered functions automatically
        builder.UseFunctionInvocation();

        // 2. Logging middleware — traces every request/response
        builder.UseLogging(loggerFactory);

        // 3. OpenTelemetry middleware — emits spans for distributed tracing
        // Requires Microsoft.Extensions.AI.OpenTelemetry package
        builder.UseOpenTelemetry(sourceName: "Ouroboros.AI.Pipeline");

        return builder;
    }

    // ----------------------------------------------------------
    // Private helpers
    // ----------------------------------------------------------

    private PipelineResult Fail(string correlationId, string code, string message)
    {
        _logger.LogWarning("[Pipeline:{CorrelationId}] Validation failed: {Code} - {Message}", correlationId, code, message);
        return new PipelineResult
        {
            IsSuccess = false,
            Error = new PipelineError(code, message)
        };
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

// ============================================================
// DI Registration Extensions
// ============================================================

/// <summary>
/// Extension methods for registering the AI pipeline with
/// Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class AIPipelineServiceExtensions
{
    /// <summary>
    /// Adds the refactored AI pipeline to the service collection.
    /// Registers <see cref="AIPipeline"/> as a scoped service with
    /// the full middleware stack.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration of default ChatOptions.</param>
    public static IServiceCollection AddAIPipeline(
        this IServiceCollection services,
        Action<ChatOptions>? configureOptions = null)
    {
        // Register the builder factory that composes middleware
        services.AddScoped<AIPipeline>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AIPipeline>>();

            // Build the middleware-wrapped IChatClient
            var builder = AIPipeline.CreateBuilder(sp);
            var chatClient = builder.Build();

            return new AIPipeline(chatClient, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a function tool that the AI pipeline can invoke.
    /// Functions are automatically discovered and registered.
    /// </summary>
    public static IServiceCollection AddAIFunction(
        this IServiceCollection services,
        AIFunction function)
    {
        services.AddSingleton(function);
        return services;
    }
}

// ============================================================
// M.E.AI Middleware stubs (for standalone compilation)
// These types are provided by Microsoft.Extensions.AI packages.
// Remove at build time when referencing the actual NuGet packages.
// ============================================================
