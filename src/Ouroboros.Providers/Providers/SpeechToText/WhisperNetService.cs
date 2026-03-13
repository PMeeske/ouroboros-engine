// <copyright file="WhisperNetService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Whisper.net;
using Whisper.net.Ggml;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Native .NET Whisper speech-to-text service using Whisper.net.
/// Provides high-performance local speech recognition without external processes.
/// </summary>
public sealed partial class WhisperNetService : ISpeechToTextService, IDisposable
{
    private readonly GgmlType _modelType;
    private readonly string _modelDirectory;
    private WhisperProcessor? _processor;
    private WhisperFactory? _factory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;
    private bool _isDisposed;

    /// <summary>
    /// Supported audio formats for Whisper.net (requires 16kHz mono WAV).
    /// </summary>
    private static readonly string[] SupportedAudioFormats = [".wav"];

    /// <inheritdoc/>
    public string ProviderName => "Whisper.net (Native)";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => SupportedAudioFormats;

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 500 * 1024 * 1024; // 500 MB

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperNetService"/> class.
    /// </summary>
    /// <param name="modelType">Whisper model type (Tiny, Base, Small, Medium, Large).</param>
    /// <param name="modelDirectory">Directory to store/load models. Defaults to user's .whisper folder.</param>
    /// <param name="lazyLoad">If true, defer model loading until first use.</param>
    public WhisperNetService(
        GgmlType modelType = GgmlType.Base,
        string? modelDirectory = null,
        bool lazyLoad = true)
    {
        _modelType = modelType;
        _modelDirectory = modelDirectory ?? GetDefaultModelDirectory();

        if (!lazyLoad)
        {
            _ = EnsureInitializedAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Creates a WhisperNetService with a specific model size string.
    /// </summary>
    /// <param name="modelSize">Model size: "tiny", "base", "small", "medium", "large".</param>
    /// <param name="modelDirectory">Directory to store/load models.</param>
    /// <returns>Configured WhisperNetService instance.</returns>
    public static WhisperNetService FromModelSize(string modelSize, string? modelDirectory = null)
    {
        GgmlType type = modelSize.ToLowerInvariant() switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large" or "large-v1" => GgmlType.LargeV1,
            "large-v2" => GgmlType.LargeV2,
            "large-v3" => GgmlType.LargeV3,
            _ => GgmlType.Base
        };

        return new WhisperNetService(type, modelDirectory);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Convert to WAV if needed
        string wavPath = filePath;
        bool needsCleanup = false;

        if (extension != ".wav")
        {
            var convertResult = await ConvertToWavAsync(filePath, ct);
            if (!convertResult.IsSuccess)
            {
                return Result<TranscriptionResult, string>.Failure(convertResult.Error!);
            }
            wavPath = convertResult.Value!;
            needsCleanup = true;
        }

        try
        {
            var initResult = await EnsureInitializedAsync(ct);
            if (!initResult.IsSuccess)
            {
                return Result<TranscriptionResult, string>.Failure(initResult.Error!);
            }

            // Read and process audio
            var samples = await ReadAudioSamplesAsync(wavPath, ct);
            if (samples == null || samples.Length == 0)
            {
                return Result<TranscriptionResult, string>.Failure("Failed to read audio samples");
            }

            // Transcribe
            var segments = new List<TranscriptionSegment>();
            var fullText = new System.Text.StringBuilder();

            await foreach (var segment in _processor!.ProcessAsync(samples, ct))
            {
                string text = segment.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    fullText.Append(text).Append(' ');
                    segments.Add(new TranscriptionSegment(
                        text,
                        segment.Start.TotalSeconds,
                        segment.End.TotalSeconds));
                }
            }

            string transcribedText = fullText.ToString().Trim();
            double? durationSeconds = segments.Count > 0 ? segments[^1].End : null;

            return Result<TranscriptionResult, string>.Success(new TranscriptionResult(
                Text: transcribedText,
                Language: options?.Language ?? "en",
                Duration: durationSeconds,
                Segments: segments));
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
        finally
        {
            if (needsCleanup && File.Exists(wavPath))
            {
                try { File.Delete(wavPath); } catch (IOException) { /* Intentional: best-effort temp file cleanup */ }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        try
        {
            await using FileStream fileStream = File.Create(tempPath);
            await audioStream.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
            fileStream.Close();

            return await TranscribeFileAsync(tempPath, options, ct);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch (IOException) { /* Intentional: best-effort temp file cleanup */ }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        await using MemoryStream stream = new(audioData);
        return await TranscribeStreamAsync(stream, fileName, options, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        // Whisper.net handles translation via language detection
        options ??= new TranscriptionOptions();
        var translationOptions = options with { Language = "en" };
        return await TranscribeFileAsync(filePath, translationOptions, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await EnsureInitializedAsync(ct);
            return result.IsSuccess;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<Result<bool, string>> EnsureInitializedAsync(CancellationToken ct)
    {
        if (_isInitialized && _processor != null)
        {
            return Result<bool, string>.Success(true);
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_isInitialized && _processor != null)
            {
                return Result<bool, string>.Success(true);
            }

            // Ensure model directory exists
            if (!Directory.Exists(_modelDirectory))
            {
                Directory.CreateDirectory(_modelDirectory);
            }

            string modelFileName = GetModelFileName(_modelType);
            string modelPath = Path.Combine(_modelDirectory, modelFileName);

            // Download model if not present
            if (!File.Exists(modelPath))
            {
                System.Diagnostics.Trace.TraceInformation("[whisper.net] Downloading {0} model...", _modelType);
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    var downloader = new WhisperGgmlDownloader(httpClient);
                    await using var modelStream = await downloader.GetGgmlModelAsync(_modelType);
                    await using var fileStream = File.Create(modelPath);
                    await modelStream.CopyToAsync(fileStream, ct);
                    System.Diagnostics.Trace.TraceInformation("[whisper.net] Model downloaded to {0}", modelPath);
                }
                catch (OperationCanceledException) { throw; }
                catch (HttpRequestException ex)
                {
                    return Result<bool, string>.Failure($"Failed to download model: {ex.Message}");
                }
                catch (IOException ex)
                {
                    return Result<bool, string>.Failure($"Failed to download model: {ex.Message}");
                }
            }

            // Create factory and processor
            _factory = WhisperFactory.FromPath(modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("auto")
                .WithThreads(Environment.ProcessorCount)
                .Build();

            _isInitialized = true;
            System.Diagnostics.Trace.TraceInformation("[whisper.net] Initialized with {0} model", _modelType);

            return Result<bool, string>.Success(true);
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
        {
            return Result<bool, string>.Failure($"Failed to initialize Whisper.net: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool, string>.Failure($"Failed to initialize Whisper.net: {ex.Message}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _processor?.Dispose();
        _factory?.Dispose();
        _initLock.Dispose();
    }
}
