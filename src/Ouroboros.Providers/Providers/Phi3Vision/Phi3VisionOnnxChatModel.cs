// <copyright file="Phi3VisionOnnxChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace Ouroboros.Providers.Phi3Vision;

/// <summary>
/// ORT-GenAI multimodal chat model backed by Microsoft's
/// <c>microsoft/Phi-3.5-vision-instruct-onnx</c> (DirectML INT4) checkpoint at
/// <c>checkpoints/onnx-phi35v-dml-int4/</c>. Used as the <c>vision</c> keyed
/// IChatClient when <c>--mode</c> is non-Ollama, replacing the moondream
/// Ollama backbone for Iaret's avatar perception (mirror recognition, frame
/// review, expression classification).
/// </summary>
/// <remarks>
/// <para>
/// Phi3v's ORT-GenAI graph requires <see cref="MultiModalProcessor"/>, not the
/// plain <see cref="Tokenizer"/> path used by Hermes-3 — the processor handles
/// both text tokenization and image preprocessing into a single
/// <see cref="NamedTensors"/> payload that <see cref="Generator.SetInputs"/>
/// consumes directly. For text-only requests we still go through the same
/// processor (it skips the image pipeline gracefully).
/// </para>
/// <para>
/// Generation is serialized via a <see cref="SemaphoreSlim"/> (ORT-GenAI's
/// <see cref="Generator"/> is not thread-safe). Image input is accepted as an
/// in-memory byte array; we materialize a temp file because OGA's
/// <see cref="Images.Load(string)"/> takes a path. The temp file is deleted
/// after inference.
/// </para>
/// </remarks>
public sealed class Phi3VisionOnnxChatModel : IDisposable
{
    private readonly Model _model;
    private readonly MultiModalProcessor _processor;
    private readonly Tokenizer _tokenizer;
    private readonly TokenizerStream _tokenizerStream;
    private readonly Phi3VisionOnnxChatModelOptions _options;
    private readonly SemaphoreSlim _genSemaphore = new(1, 1);
    private readonly ILogger<Phi3VisionOnnxChatModel>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Phi3VisionOnnxChatModel"/> class.
    /// </summary>
    /// <param name="modelPath">Directory containing model.onnx + genai_config.json + processor_config.json.</param>
    /// <param name="options">Sampling and execution settings.</param>
    /// <param name="logger">Optional structured logger.</param>
    public Phi3VisionOnnxChatModel(
        string modelPath,
        Phi3VisionOnnxChatModelOptions options,
        ILogger<Phi3VisionOnnxChatModel>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(options);
        if (!Directory.Exists(modelPath))
        {
            throw new DirectoryNotFoundException(
                $"Phi-3.5-vision ONNX model directory not found: {modelPath}");
        }

        _options = options;
        _logger = logger;

        _model = new Model(modelPath);
        _processor = new MultiModalProcessor(_model);
        _tokenizer = new Tokenizer(_model);
        _tokenizerStream = _tokenizer.CreateStream();

        logger?.LogInformation(
            "[Phi3Vision] Model + MultiModalProcessor loaded (path={Path}, ep={Ep})",
            modelPath,
            options.ExecutionProvider);
    }

    /// <summary>
    /// Generates a response to <paramref name="prompt"/>, optionally conditioned on
    /// <paramref name="imageBytes"/>. Pass <see langword="null"/> for text-only
    /// inference.
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        byte[]? imageBytes,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(prompt);

        string formatted = ApplyPhi3Template(prompt, hasImage: imageBytes is not null && imageBytes.Length > 0);

        await _genSemaphore.WaitAsync(ct).ConfigureAwait(false);
        string? tempImagePath = null;
        try
        {
            using Images? images = LoadImagesIfPresent(imageBytes, out tempImagePath);
            using NamedTensors processed = _processor.ProcessImages(formatted, images);

            using GeneratorParams gp = new(_model);
            gp.SetSearchOption("max_length", _options.MaxLength);
            gp.SetSearchOption("temperature", _options.Temperature);
            gp.SetSearchOption("top_p", _options.TopP);
            gp.SetSearchOption("top_k", _options.TopK);

            using Generator gen = new(_model, gp);
            gen.SetInputs(processed);

            StringBuilder sb = new();
            while (!gen.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                gen.GenerateNextToken();
                ReadOnlySpan<int> seq = gen.GetSequence(0);
                if (seq.Length == 0) continue;

                int latest = seq[^1];
                string piece = _tokenizerStream.Decode(latest);
                if (!string.IsNullOrEmpty(piece))
                {
                    sb.Append(piece);
                }
            }

            return sb.ToString().TrimEnd();
        }
        finally
        {
            if (tempImagePath is not null)
            {
                try { File.Delete(tempImagePath); }
                catch (IOException) { /* best effort — temp cleanup */ }
                catch (UnauthorizedAccessException) { /* best effort */ }
            }

            _genSemaphore.Release();
        }
    }

    /// <summary>
    /// Phi-3.5 vision uses the <c>&lt;|user|&gt;</c>/<c>&lt;|end|&gt;</c>/<c>&lt;|assistant|&gt;</c>
    /// turn template. Image slot (<c>&lt;|image_1|&gt;</c>) is added before the user
    /// text when an image is present; the processor wires it to the actual
    /// preprocessed image tensor.
    /// </summary>
    private static string ApplyPhi3Template(string prompt, bool hasImage)
    {
        StringBuilder sb = new();
        sb.Append("<|user|>\n");
        if (hasImage)
        {
            sb.Append("<|image_1|>\n");
        }

        sb.Append(prompt);
        sb.Append("<|end|>\n<|assistant|>\n");
        return sb.ToString();
    }

    /// <summary>
    /// OGA's <see cref="Images.Load(string)"/> takes a file path; materialize
    /// in-memory bytes to a temp file so callers can stream from anywhere.
    /// Returns null when no image is supplied (pure text inference).
    /// </summary>
    private static Images? LoadImagesIfPresent(byte[]? imageBytes, out string? tempPath)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            tempPath = null;
            return null;
        }

        // Phi3v expects a real on-disk image; pick an extension OGA recognizes.
        // The processor decodes by content, not by extension, but OGA's loader
        // wants a file it can stat.
        tempPath = Path.Combine(Path.GetTempPath(), $"phi3v-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(tempPath, imageBytes);
        return Images.Load([tempPath]);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tokenizerStream.Dispose();
        _tokenizer.Dispose();
        _processor.Dispose();
        _model.Dispose();
        _genSemaphore.Dispose();
    }
}
