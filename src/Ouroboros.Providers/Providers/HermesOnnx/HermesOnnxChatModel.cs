// <copyright file="HermesOnnxChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.ML.Tokenizers;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Providers.HermesOnnx;

/// <summary>
/// <see cref="IChatCompletionModel"/> implementation backed by
/// <c>Microsoft.ML.OnnxRuntimeGenAI.DirectML</c> for the locally retrained Hermes-4
/// INT4 model at <c>checkpoints/onnx-hermes/hermes-4.3-36b-onnx-int4/</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Path C tokenizer:</b> ORT-GenAI's bundled <c>Tokenizer</c> uses an std::regex
/// backend that doesn't support <c>\p{L}</c>/<c>\p{N}</c> Unicode property classes
/// — incompatible with this Hermes export's HF tokenizer.json. We bypass it
/// entirely by parsing tokenizer.json into separate vocab/merges streams and
/// loading via <see cref="BpeTokenizer"/> from <c>Microsoft.ML.Tokenizers</c>
/// (.NET's regex supports <c>\p{}</c> natively).
/// </para>
/// <para>
/// Constructor invokes <see cref="GenaiConfigRetargeter.EnsureDirectMlProvider(string, ILogger?)"/>
/// once to rewrite the shipped CUDA provider config to DirectML before
/// <c>new Model()</c>. Generation is serialized via <see cref="SemaphoreSlim"/>
/// (ORT-GenAI <c>Generator</c> is not thread-safe). Cancellation is checked between
/// tokens (ORT-GenAI 0.13.x <c>GenerateNextToken</c> is synchronous + uninterruptible).
/// </para>
/// </remarks>
public sealed class HermesOnnxChatModel : IChatCompletionModel, IDisposable
{
    private readonly Model _model;
    private readonly BpeTokenizer _tokenizer;
    private readonly int _eosTokenId;
    private readonly HermesOnnxChatModelOptions _options;
    private readonly SemaphoreSlim _genSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HermesOnnxChatModel"/> class.
    /// </summary>
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

        // Idempotent CUDA -> DML provider retarget.
        GenaiConfigRetargeter.EnsureDirectMlProvider(modelPath, logger);

        _model = new Model(modelPath);
        (_tokenizer, _eosTokenId) = LoadBpeTokenizerFromHuggingFaceJson(modelPath);

        logger?.LogInformation(
            "[HermesOnnx PathC] Model + ML.Tokenizers BpeTokenizer loaded (eos_id={EosId}, path={Path})",
            _eosTokenId,
            modelPath);
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
            IReadOnlyList<int> inputIds = _tokenizer.EncodeToIds(templated);
            int[] inputIdsArr = inputIds is int[] a ? a : inputIds.ToArray();

            using GeneratorParams gp = new(_model);
            gp.SetSearchOption("max_length", _options.MaxLength);
            gp.SetSearchOption("temperature", _options.Temperature);
            gp.SetSearchOption("top_p", _options.TopP);
            gp.SetSearchOption("top_k", _options.TopK);

            using Generator g = new(_model, gp);
            g.AppendTokens(inputIdsArr);

            int promptLength = inputIdsArr.Length;
            List<int> outputIds = new(capacity: 256);
            while (!g.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                g.GenerateNextToken();
                ReadOnlySpan<int> seq = g.GetSequence(0);
                if (seq.Length <= promptLength)
                {
                    continue;
                }

                int latest = seq[^1];
                if (latest == _eosTokenId)
                {
                    break;
                }

                outputIds.Add(latest);
            }

            return _tokenizer.Decode(outputIds);
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

    /// <summary>
    /// Parses HuggingFace <c>tokenizer.json</c> (vocab + merges combined in one file)
    /// into separate vocab/merges streams + special-token map, then constructs a
    /// <see cref="BpeTokenizer"/>. Bypasses ORT-GenAI's std::regex backend entirely —
    /// .NET's regex supports <c>\p{L}</c>/<c>\p{N}</c> natively.
    /// </summary>
    private static (BpeTokenizer Tokenizer, int EosTokenId) LoadBpeTokenizerFromHuggingFaceJson(string modelPath)
    {
        string tokenizerJsonPath = Path.Combine(modelPath, "tokenizer.json");
        if (!File.Exists(tokenizerJsonPath))
        {
            throw new FileNotFoundException(
                $"tokenizer.json not found at {tokenizerJsonPath}");
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(tokenizerJsonPath));
        JsonElement root = doc.RootElement;
        JsonElement modelEl = root.GetProperty("model");

        // Build vocab.json stream — { "token": id, ... } shape ML.Tokenizers expects.
        JsonElement vocabEl = modelEl.GetProperty("vocab");
        MemoryStream vocabStream = new();
        using (Utf8JsonWriter w = new(vocabStream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            foreach (JsonProperty p in vocabEl.EnumerateObject())
            {
                w.WriteNumber(p.Name, p.Value.GetInt32());
            }

            w.WriteEndObject();
        }

        vocabStream.Position = 0;

        // Build merges.txt stream — "#version: 0.2\n{a} {b}\n..." shape.
        JsonElement mergesEl = modelEl.GetProperty("merges");
        MemoryStream mergesStream = new();
        StreamWriter mw = new(mergesStream, new UTF8Encoding(false), leaveOpen: true);
        try
        {
            mw.WriteLine("#version: 0.2");
            foreach (JsonElement pair in mergesEl.EnumerateArray())
            {
                mw.WriteLine(pair.GetString());
            }
        }
        finally
        {
            mw.Flush();
            mw.Dispose();
        }

        mergesStream.Position = 0;

        // Special tokens from added_tokens block (incl. <seed:bos>, <|eot_id|>, etc.).
        Dictionary<string, int> specialTokens = new(StringComparer.Ordinal);
        if (root.TryGetProperty("added_tokens", out JsonElement addedEl)
            && addedEl.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement t in addedEl.EnumerateArray())
            {
                if (t.TryGetProperty("content", out JsonElement contentEl)
                    && t.TryGetProperty("id", out JsonElement idEl)
                    && contentEl.ValueKind == JsonValueKind.String
                    && idEl.ValueKind == JsonValueKind.Number)
                {
                    string? name = contentEl.GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        specialTokens[name] = idEl.GetInt32();
                    }
                }
            }
        }

        BpeTokenizer tokenizer = BpeTokenizer.Create(
            vocabStream,
            mergesStream,
            preTokenizer: null,
            normalizer: null,
            specialTokens: specialTokens);

        // Hermes 4 / Llama-3 family eos. Fall back to "<|endoftext|>" or 2 if missing.
        int eosId =
            specialTokens.TryGetValue("<|eot_id|>", out int eot) ? eot
            : specialTokens.TryGetValue("<|endoftext|>", out int eod) ? eod
            : 2;

        return (tokenizer, eosId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _model.Dispose();
        _genSemaphore.Dispose();
    }
}
