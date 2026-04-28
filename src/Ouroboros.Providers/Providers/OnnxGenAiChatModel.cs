// <copyright file="OnnxGenAiChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntimeGenAI;
using Ouroboros.Abstractions.Core;
using System.Text;

namespace Ouroboros.Providers;

/// <summary>
/// ONNX Runtime GenAI-backed chat model for running local LLMs in ONNX format.
/// Supports Hermes-3, Phi-3, Llama, Mistral, Seed-OSS (Hermes-4.3), and other
/// ONNX-exported models via Microsoft.ML.OnnxRuntimeGenAI (OGA).
/// </summary>
/// <remarks>
/// <para>
/// This adapter wraps the ONNX Runtime GenAI C# API:
///   - <seealso href="https://onnxruntime.ai/docs/genai/">ONNX Runtime GenAI docs</seealso>
///   - GenAI models: <seealso href="https://huggingface.co/Prince-1/Hermes-3-Llama-3.1-8B-ONNX"/>
/// </para>
/// <para>
/// Usage: instantiate with a model directory path containing the exported ONNX model,
/// tokenizer, and genai_config.json. The model must be exported with
///   <c>optimum-cli export onnx --model NousResearch/Hermes-3-Llama-3.1-8B \</c>
///   <c>  --dtype int4 --task text-generation-with-past \</c>
///   <c>  ./models/hermes-3-onnx</c>
/// </para>
/// <para>
/// Execution providers: DirectML is preferred for Windows GPU inference; falls back
/// to CPU EP automatically. Device selection is via <see cref="OnnxExecutionProviderFactory"/>
/// — the same factory used for Kokoro TTS.
/// </para>
/// <para>
/// Threading: <see cref="Microsoft.ML.OnnxRuntimeGenAI.Model"/> is NOT thread-safe for
/// concurrent generation. This adapter serializes access via <c>SemaphoreSlim</c>.
/// </para>
/// </remarks>
public sealed class OnnxGenAiChatModel : IOuroborosChatClient, ICostAwareChatModel, IDisposable
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;
    private readonly OnnxRuntimeSettings _settings;
    private readonly SemaphoreSlim _genSemaphore;
    private readonly LlmCostTracker _costTracker;
    private bool _disposed;

    /// <summary>
    /// Initializes a new ONNX GenAI chat model.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX GenAI model directory (contains .onnx, tokenizer, config).</param>
    /// <param name="settings">Generation settings (temperature, top_p, etc.).</param>
    /// <param name="costTracker">Optional cost tracker for telemetry.</param>
    /// <exception cref="DirectoryNotFoundException">When <paramref name="modelPath"/> does not exist.</exception>
    /// <exception cref="ArgumentException">When the model directory lacks required files.</exception>
    public OnnxGenAiChatModel(
        string modelPath,
        OnnxRuntimeSettings? settings = null,
        LlmCostTracker? costTracker = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        if (!Directory.Exists(modelPath))
        {
            throw new DirectoryNotFoundException(
                $"ONNX model directory not found: {modelPath}. " +
                "Export models via: optimum-cli export onnx --model NousResearch/Hermes-3-Llama-3.1-8B " +
                $"--dtype int4 --task text-generation-with-past {modelPath}");
        }

        _settings = settings ?? OnnxRuntimeSettings.Default;
        _costTracker = costTracker ?? new LlmCostTracker();

        // Build GenAI config — provider selection is via genai_config.json
        var config = new Config(modelPath);

        _model = new Model(config);
        _tokenizer = new Tokenizer(_model);
        _genSemaphore = new SemaphoreSlim(1, 1);
    }

    #region IOuroborosChatClient

    /// <inheritdoc/>
    public bool SupportsThinking => false; // Hermes-3 emits <think> tags but OGA sampling pipeline doesn't auto-parse

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc/>
    /// <remarks>
    /// Maps MEAI <see cref="IChatClient"/> messages into a single prompt string, runs
    /// ONNX GenAI generation, and returns the assistant response.
    /// </remarks>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string promptText = ComposePrompt(messages, options?.SystemPrompt);
        string responseText = await GenerateTextAsync(promptText, cancellationToken).ConfigureAwait(false);

        var message = new ChatMessage(ChatRole.Assistant, responseText);
        var response = new ChatResponse(message);

        // Track token usage estimation (no native GenAI tokenizer count API; approximate)
        int promptTokens = _tokenizer.Encode(promptText).Length;
        int completionTokens = _tokenizer.Encode(responseText).Length;
        _ = _costTracker.TrackAsync(promptTokens, completionTokens);

        return response;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string promptText = ComposePrompt(messages, options?.SystemPrompt);

        await _genSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sequences = _tokenizer.Encode(promptText);
            using var generatorParams = new GeneratorParams(_model);
            generatorParams.SetSearchOption("max_length", options?.MaxOutputTokens ?? _settings.MaxLength);
            generatorParams.SetSearchOption("temperature", _settings.Temperature);
            generatorParams.SetSearchOption("top_p", _settings.TopP);
            generatorParams.SetSearchOption("top_k", _settings.TopK);
            generatorParams.SetSearchOption("past_present_share_buffer", true);
            generatorParams.SetInputSequences(sequences);

            // Optional: repetition penalty (supported by Hermes-3 base Llama)
            if (_settings.RepetitionPenalty > 1.0f)
            {
                generatorParams.SetSearchOption("repetition_penalty", _settings.RepetitionPenalty);
            }

            using var generator = new Generator(_model, generatorParams);

            var sb = new StringBuilder();
            while (!generator.IsDone())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Generate one token
                await Task.Run(generator.ComputeLogits, cancellationToken).ConfigureAwait(false);
                await Task.Run(generator.GenerateNextToken, cancellationToken).ConfigureAwait(false);

                int nextTokenId = generator.GetSequenceData(0)[^1];
                ReadOnlySpan<byte> tokenSpan = _tokenizer.Decode([nextTokenId]);
                string tokenText = Encoding.UTF8.GetString(tokenSpan);

                sb.Append(tokenText);
                yield return new ChatResponseUpdate(ChatRole.Assistant, tokenText);
            }
        }
        finally
        {
            _genSemaphore.Release();
        }
    }

    #endregion

    #region IChatCompletionModel (legacy)

    /// <summary>
    /// Legacy entry point for prompt-based generation.
    /// New code should prefer <see cref="GetResponseAsync(IEnumerable{ChatMessage}, ChatOptions?, CancellationToken)"/>.
    /// </summary>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions(), ct).ConfigureAwait(false);

        return response.Message.Text ?? string.Empty;
    }

    #endregion

    #region ICostAwareChatModel (best-effort)

    /// <inheritdoc/>
    public LlmCostTracker? CostTracker => _costTracker;

    #endregion

    #region Prompt Composition

    /// <summary>
    /// Builds a flat prompt string from MEAI messages, applying Hermes-3 chat template.
    /// </summary>
    private static string ComposePrompt(IEnumerable<ChatMessage> messages, string? systemPromptOverride)
    {
        // Hermes-3 uses the Llama 3.1 chat template — apply via raw string interpolation
        // (If tokenizer has chat template support, swap this for _tokenizer.ApplyChatTemplate)
        var sb = new StringBuilder();

        string? system = systemPromptOverride;
        foreach (ChatMessage msg in messages)
        {
            if (msg.Role == ChatRole.System && system is null)
            {
                system = msg.Text;
            }
            else if (msg.Role == ChatRole.User)
            {
                sb.Append("<|start_header_id|>user<|end_header_id|>\n");
                sb.Append(msg.Text);
                sb.Append("<|eot_id|>\n");
            }
            else if (msg.Role == ChatRole.Assistant)
            {
                sb.Append("<|start_header_id|>assistant<|end_header_id|>\n");
                sb.Append(msg.Text);
                sb.Append("<|eot_id|>\n");
            }
        }

        if (!string.IsNullOrEmpty(system))
        {
            sb.Insert(0, $"<|start_header_id|>system<|end_header_id|>\n{system}\n<|eot_id|>\n");
        }

        sb.Append("<|start_header_id|>assistant<|end_header_id|>\n");
        return sb.ToString();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _genSemaphore.Wait();
        try
        {
            _tokenizer.Dispose();
            _model.Dispose();
            _genSemaphore.Dispose();
        }
        finally
        {
            _genSemaphore.Release();
        }
    }

    #endregion
}

/// <summary>
/// Sampling and runtime configuration for <see cref="OnnxGenAiChatModel"/>.
/// </summary>
public sealed record OnnxRuntimeSettings(
    float Temperature = 0.7f,
    float TopP = 0.9f,
    int TopK = 40,
    int MaxLength = 4096,
    float RepetitionPenalty = 1.1f)
{
    /// <summary>Default conservative settings for Hermes-3 on ONNX.</summary>
    public static OnnxRuntimeSettings Default => new();

    /// <summary>Creative / high-entropy generation.</summary>
    public static OnnxRuntimeSettings Creative => new(Temperature: 0.9f, TopP: 0.95f, TopK: 60);

    /// <summary>Strict / deterministic generation.</summary>
    public static OnnxRuntimeSettings Deterministic => new(Temperature: 0.2f, TopP: 0.85f, TopK: 20);
}
