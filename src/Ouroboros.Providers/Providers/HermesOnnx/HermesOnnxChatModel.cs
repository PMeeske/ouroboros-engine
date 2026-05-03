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
    private readonly HashSet<int> _eosTokenIds;
    private readonly HermesOnnxChatModelOptions _options;
    private readonly SemaphoreSlim _genSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Matches any literal <c>&lt;|...|&gt;</c>-shaped pseudo-token the model may emit
    /// as text instead of as a real special token. Anchored on the rare bracket
    /// pair so normal punctuation is untouched.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex SpecialTokenLiteralPattern =
        new(@"<\|[^|<>]*\|>", System.Text.RegularExpressions.RegexOptions.Compiled);

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

        // Idempotent CUDA -> {DML|CPU} provider retarget. CPU EP is the documented
        // fallback when the DML kernel rejects the graph (DmlFusedNode E_INVALIDARG
        // on builder-incompatible exports — see docs/hermes-onnx-rebuild.md).
        GenaiConfigRetargeter.EnsureProvider(modelPath, options.ExecutionProvider, logger);

        // Build tokenizer FIRST (cheap, all managed). If it throws, no native Model
        // is allocated — no Generators::Model leak. If Model ctor then throws, the
        // tokenizer has no native handle to leak (BpeTokenizer is pure managed).
        (_tokenizer, _eosTokenIds) = LoadBpeTokenizerFromHuggingFaceJson(modelPath);
        _model = new Model(modelPath);

        logger?.LogInformation(
            "[HermesOnnx PathC] Model + ML.Tokenizers BpeTokenizer loaded (eos_ids=[{EosIds}], path={Path})",
            string.Join(",", _eosTokenIds),
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
                if (_eosTokenIds.Contains(latest))
                {
                    break;
                }

                outputIds.Add(latest);
            }

            string decoded = _tokenizer.Decode(outputIds);

            // Llama-3.1-Instruct sometimes spells out chat-template tokens as
            // literal text in its response ("<|eot_id|>", "<|end_header_id|>",
            // etc.) instead of emitting them as the actual special tokens. It
            // also occasionally invents fake ones ("<|implied_id|>"). Strip any
            // <|...|> shaped string before returning — they're a model behavior
            // tic, not semantic content.
            decoded = SpecialTokenLiteralPattern.Replace(decoded, string.Empty);

            return decoded.TrimEnd();
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
    private static (BpeTokenizer Tokenizer, HashSet<int> EosTokenIds) LoadBpeTokenizerFromHuggingFaceJson(string modelPath)
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

        // Vocabulary: HF tokenizer.json's model.vocab is { "token": id, ... }.
        JsonElement vocabEl = modelEl.GetProperty("vocab");
        Dictionary<string, int> vocab = new(StringComparer.Ordinal);
        foreach (JsonProperty p in vocabEl.EnumerateObject())
        {
            vocab[p.Name] = p.Value.GetInt32();
        }

        // Merges: HF tokenizer.json's model.merges is EITHER an array of strings
        // ("a b") OR an array of two-element arrays (["a", "b"]). Both shapes appear
        // in the wild; recent HF tokenizers (>=0.13) write the array form. Normalize
        // both to "a b" for ML.Tokenizers BpeOptions.Merges.
        JsonElement mergesEl = modelEl.GetProperty("merges");
        List<string> merges = new(capacity: mergesEl.GetArrayLength());
        foreach (JsonElement pair in mergesEl.EnumerateArray())
        {
            if (pair.ValueKind == JsonValueKind.String)
            {
                merges.Add(pair.GetString()!);
            }
            else if (pair.ValueKind == JsonValueKind.Array && pair.GetArrayLength() == 2)
            {
                string a = pair[0].GetString() ?? string.Empty;
                string b = pair[1].GetString() ?? string.Empty;
                merges.Add($"{a} {b}");
            }
        }

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

        BpeOptions bpeOptions = new(vocab)
        {
            Merges = merges,
            SpecialTokens = specialTokens,
            ByteLevel = true,  // Llama-3-family byte-level BPE
        };

        BpeTokenizer tokenizer = BpeTokenizer.Create(bpeOptions);

        // EOS tokens — read genai_config.json's eos_token_id (int OR int array).
        // Llama-3.1 declares three: <|end_of_text|>=128001, <|eom_id|>=128008,
        // <|eot_id|>=128009. Stopping on only one leaks the others into output.
        HashSet<int> eosIds = new();
        string genaiConfigPath = Path.Combine(modelPath, "genai_config.json");
        if (File.Exists(genaiConfigPath))
        {
            using JsonDocument cfgDoc = JsonDocument.Parse(File.ReadAllText(genaiConfigPath));
            if (cfgDoc.RootElement.TryGetProperty("model", out JsonElement modelCfg)
                && modelCfg.TryGetProperty("eos_token_id", out JsonElement eosEl))
            {
                if (eosEl.ValueKind == JsonValueKind.Number)
                {
                    eosIds.Add(eosEl.GetInt32());
                }
                else if (eosEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement e in eosEl.EnumerateArray())
                    {
                        if (e.ValueKind == JsonValueKind.Number)
                        {
                            eosIds.Add(e.GetInt32());
                        }
                    }
                }
            }
        }

        // Always include the canonical Llama-3 turn-end markers if known to the
        // tokenizer — covers cases where genai_config.json is sparse (e.g. legacy
        // Hermes 4.3 36B retrain ships with only <|eot_id|>).
        if (specialTokens.TryGetValue("<|eot_id|>", out int eot)) { eosIds.Add(eot); }
        if (specialTokens.TryGetValue("<|end_of_text|>", out int eot2)) { eosIds.Add(eot2); }
        if (specialTokens.TryGetValue("<|eom_id|>", out int eom)) { eosIds.Add(eom); }
        if (specialTokens.TryGetValue("<|endoftext|>", out int eod)) { eosIds.Add(eod); }
        if (eosIds.Count == 0) { eosIds.Add(2); }  // last-resort GPT-style EOS

        return (tokenizer, eosIds);
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
