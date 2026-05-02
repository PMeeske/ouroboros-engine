// <copyright file="HermesOnnxChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Providers.HermesOnnx;

/// <summary>
/// <see cref="IChatCompletionModel"/> implementation backed by
/// <c>Microsoft.ML.OnnxRuntimeGenAI.DirectML</c> for the locally retrained Hermes-4
/// INT4 model at <c>checkpoints/onnx-hermes/hermes-4.3-36b-onnx-int4/</c>.
/// </summary>
/// <remarks>
/// <para>
/// Constructor invokes <see cref="GenaiConfigRetargeter.EnsureDirectMlProvider(string, ILogger?)"/>
/// once to rewrite the shipped CUDA provider config to DirectML before
/// <c>new Model()</c>. Generation is serialized via <see cref="SemaphoreSlim"/>
/// (ORT-GenAI <c>Generator</c> is not thread-safe). Cancellation is checked between
/// tokens (ORT-GenAI 0.13.x <c>GenerateNextToken</c> is synchronous + uninterruptible).
/// </para>
/// <para>
/// <b>Future LUID pinning (Plan 263-01 deviation, see SUMMARY.md):</b> The plan
/// specified an <c>ISharedOrtDmlSessionFactory</c> constructor parameter reserved for
/// future Path B (Section 7) device-id binding. That dependency would cause a
/// circular reference (<c>Ouroboros.Tensor</c> already references
/// <c>Ouroboros.Providers</c>), so the parameter was dropped for Plan 263-01.
/// ORT-GenAI 0.13.x reads provider config from <c>genai_config.json</c> only —
/// the retargeter writes <c>{ "name": "DML", "options": [] }</c> without explicit
/// deviceId, letting ORT-GenAI pick the default DXGI adapter. A follow-up phase
/// can wire LUID pinning by either lifting the abstraction to Foundation or
/// performing the wiring at the App layer.
/// </para>
/// </remarks>
public sealed class HermesOnnxChatModel : IChatCompletionModel, IDisposable
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;
    private readonly HermesOnnxChatModelOptions _options;
    private readonly SemaphoreSlim _genSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HermesOnnxChatModel"/> class.
    /// </summary>
    /// <param name="modelPath">Absolute path to the model directory containing
    /// <c>model.onnx</c>, <c>model.onnx.data</c>, <c>tokenizer.json</c>, <c>genai_config.json</c>.</param>
    /// <param name="options">Sampling and runtime options.</param>
    /// <param name="logger">Optional logger for retarget + load telemetry.</param>
    public HermesOnnxChatModel(
        string modelPath,
        HermesOnnxChatModelOptions options,
        ILogger<HermesOnnxChatModel>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(options);
        if (!Directory.Exists(modelPath))
        {
            throw new DirectoryNotFoundException(
                $"Hermes ONNX model directory not found: {modelPath}");
        }

        _options = options;

        // Idempotent CUDA -> DML provider retarget (Path A, RESEARCH.md Section 7).
        GenaiConfigRetargeter.EnsureDirectMlProvider(modelPath, logger);

        _model = new Model(modelPath);
        _tokenizer = new Tokenizer(_model);
        logger?.LogInformation("[HermesOnnx] Model loaded from {Path}", modelPath);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(prompt);

        string templated = ApplyLlama3Template(prompt);

        await _genSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using Sequences inputTokens = _tokenizer.Encode(templated);
            using GeneratorParams generatorParams = new(_model);
            generatorParams.SetSearchOption("max_length", _options.MaxLength);
            generatorParams.SetSearchOption("temperature", _options.Temperature);
            generatorParams.SetSearchOption("top_p", _options.TopP);
            generatorParams.SetSearchOption("top_k", _options.TopK);

            using Generator generator = new(_model, generatorParams);
            generator.AppendTokens(inputTokens[0]);
            using TokenizerStream stream = _tokenizer.CreateStream();

            StringBuilder output = new();
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                // ORT-GenAI 0.13.x: GenerateNextToken is synchronous + uninterruptible.
                // CT check happens BETWEEN tokens, not during. (RESEARCH.md Pitfall 7)
                generator.GenerateNextToken();

                ReadOnlySpan<int> seq = generator.GetSequence(0);
                int lastToken = seq[^1];
                output.Append(stream.Decode(lastToken));
            }

            return output.ToString();
        }
        finally
        {
            _genSemaphore.Release();
        }
    }

    private static string ApplyLlama3Template(string prompt)
    {
        StringBuilder sb = new();
        sb.Append("<|start_header_id|>user<|end_header_id|>\n");
        sb.Append(prompt);
        sb.Append("<|eot_id|>\n");
        sb.Append("<|start_header_id|>assistant<|end_header_id|>\n");
        return sb.ToString();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tokenizer.Dispose();
        _model.Dispose();
        _genSemaphore.Dispose();
    }
}
