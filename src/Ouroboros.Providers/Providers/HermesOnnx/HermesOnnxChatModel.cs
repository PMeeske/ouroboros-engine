// <copyright file="HermesOnnxChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
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

        // Refuse known-incompatible tokenizer classes BEFORE invoking new Tokenizer(model).
        // ORT-GenAI's Tokenizer ctor allocates a native handle, then validates and throws
        // on unsupported classes — leaving a half-initialized object whose finalizer
        // crashes the process with 0xC0000005 in OgaDestroyTokenizer. Pre-checking
        // managed-side keeps the failure clean: no native handle ever allocated.
        EnsureTokenizerClassSupported(modelPath);

        // Construct Model first; if Tokenizer ctor throws (e.g., regex/vocab compat —
        // see todo 2026-05-02-hermes-onnx-tokenizer-class-not-supported.md), dispose
        // Model so the OGA "Generators::Model leaked" warning doesn't fire.
        Model? model = null;
        try
        {
            model = new Model(modelPath);
            Tokenizer tok = new(model);
            _model = model;
            _tokenizer = tok;
            model = null; // ownership transferred — skip cleanup below
        }
        finally
        {
            model?.Dispose();
        }

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

    /// <summary>
    /// Reads tokenizer_config.json and refuses known-incompatible tokenizer classes
    /// before invoking ORT-GenAI's Tokenizer ctor. Prevents the 0xC0000005 finalizer
    /// crash that follows a Tokenizer ctor throw — the OGA wrapper allocates a native
    /// handle inside its ctor, doesn't clean it up on validation failure, and the
    /// half-initialized Tokenizer's finalizer eventually AVs in OgaDestroyTokenizer.
    /// </summary>
    private static void EnsureTokenizerClassSupported(string modelPath)
    {
        string configPath = Path.Combine(modelPath, "tokenizer_config.json");
        if (!File.Exists(configPath))
        {
            return; // nothing to check; let OGA decide
        }

        string json = File.ReadAllText(configPath);
        using JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tokenizer_class", out JsonElement classElem)
            || classElem.ValueKind != JsonValueKind.String)
        {
            return;
        }

        string tokenizerClass = classElem.GetString() ?? string.Empty;
        if (string.Equals(tokenizerClass, "TokenizersBackend", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Hermes ONNX tokenizer_class='{tokenizerClass}' is not supported by ORT-GenAI's bundled "
                + "Tokenizer (HF tokenizers-library serialization with \\p{L}/\\p{N} regex syntax that "
                + "ORT-GenAI's std::regex backend cannot compile). Path C adapter via "
                + "Microsoft.ML.Tokenizers is required to use this model. See "
                + ".planning/todos/pending/2026-05-02-hermes-onnx-tokenizer-class-not-supported.md.");
        }
    }
}
