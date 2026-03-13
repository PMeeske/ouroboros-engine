// <copyright file="WhisperSpeechToTextService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// OpenAI Whisper API-based speech-to-text service.
/// Supports transcription and translation of audio files.
/// </summary>
public sealed class WhisperSpeechToTextService : ISpeechToTextService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly bool _ownsClient;

    /// <summary>
    /// Supported audio formats for Whisper API.
    /// </summary>
    private static readonly string[] SupportedAudioFormats =
    [
        ".flac", ".m4a", ".mp3", ".mp4", ".mpeg", ".mpga", ".oga", ".ogg", ".wav", ".webm",
    ];

    /// <inheritdoc/>
    public string ProviderName => "OpenAI Whisper";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => SupportedAudioFormats;

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 25 * 1024 * 1024; // 25 MB limit

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperSpeechToTextService"/> class.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="endpoint">API endpoint (defaults to OpenAI).</param>
    /// <param name="model">Whisper model to use (defaults to whisper-1).</param>
    /// <param name="httpClient">Optional HTTP client.</param>
    public WhisperSpeechToTextService(
        string apiKey,
        string? endpoint = null,
        string model = "whisper-1",
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        _apiKey = apiKey;
        _endpoint = endpoint ?? "https://api.openai.com/v1";
        _model = model;
        _ownsClient = httpClient == null;
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler();
        try
        {
            handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
            var client = new HttpClient(handler, disposeHandler: true);
            handler = null!; // Ownership transferred to HttpClient
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
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
        if (!SupportedAudioFormats.Contains(extension))
        {
            return Result<TranscriptionResult, string>.Failure(
                $"Unsupported audio format: {extension}. Supported formats: {string.Join(", ", SupportedAudioFormats)}");
        }

        FileInfo fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            return Result<TranscriptionResult, string>.Failure(
                $"File too large: {fileInfo.Length / (1024 * 1024):F2} MB. Maximum size: {MaxFileSizeBytes / (1024 * 1024)} MB");
        }

        try
        {
            using FileStream stream = File.OpenRead(filePath);
            return await TranscribeStreamAsync(stream, Path.GetFileName(filePath), options, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to read file: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        return await SendTranscriptionRequestAsync(
            $"{_endpoint}/audio/transcriptions",
            audioStream,
            fileName,
            options,
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        using MemoryStream stream = new MemoryStream(audioData);
        return await TranscribeStreamAsync(stream, fileName, options, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        try
        {
            using FileStream stream = File.OpenRead(filePath);
            return await SendTranscriptionRequestAsync(
                $"{_endpoint}/audio/translations",
                stream,
                Path.GetFileName(filePath),
                options,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to read file: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return false;
        }

        try
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{_endpoint}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the HTTP client if owned.
    /// </summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static Result<TranscriptionResult, string> ParseVerboseJsonResponse(string json)
    {
        try
        {
            WhisperVerboseResponse? response = JsonSerializer.Deserialize<WhisperVerboseResponse>(json);
            if (response == null)
            {
                return Result<TranscriptionResult, string>.Failure("Failed to parse Whisper response");
            }

            List<TranscriptionSegment>? segments = response.Segments?
                .Select(s => new TranscriptionSegment(
                    s.Text ?? string.Empty,
                    s.Start,
                    s.End,
                    null))
                .ToList();

            return Result<TranscriptionResult, string>.Success(
                new TranscriptionResult(
                    response.Text ?? string.Empty,
                    response.Language,
                    response.Duration,
                    segments));
        }
        catch (JsonException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to parse response: {ex.Message}");
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".mp4" or ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".ogg" or ".oga" => "audio/ogg",
            ".flac" => "audio/flac",
            ".mpeg" or ".mpga" => "audio/mpeg",
            _ => "application/octet-stream",
        };
    }

    private async Task<Result<TranscriptionResult, string>> SendTranscriptionRequestAsync(
        string url,
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options,
        CancellationToken ct)
    {
        try
        {
            using MultipartFormDataContent content = new MultipartFormDataContent();

            // Add the audio file (ownership transferred to MultipartFormDataContent)
            AddStreamPart(content, audioStream, Path.GetExtension(fileName), fileName);

            // Add model (ownership transferred to MultipartFormDataContent)
            AddStringPart(content, _model, "model");

            // Add optional parameters
            options ??= new TranscriptionOptions();

            if (!string.IsNullOrEmpty(options.Language))
            {
                AddStringPart(content, options.Language, "language");
            }

            if (!string.IsNullOrEmpty(options.Prompt))
            {
                AddStringPart(content, options.Prompt, "prompt");
            }

            if (options.Temperature.HasValue)
            {
                AddStringPart(content, options.Temperature.Value.ToString("F2"), "temperature");
            }

            // Request verbose_json for rich response
            string responseFormat = options.ResponseFormat == "text" ? "text" : "verbose_json";
            AddStringPart(content, responseFormat, "response_format");

            if (!string.IsNullOrEmpty(options.TimestampGranularity))
            {
                AddStringPart(content, options.TimestampGranularity, "timestamp_granularities[]");
            }

            // Send request
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<TranscriptionResult, string>.Failure(
                    $"Whisper API error ({response.StatusCode}): {responseText}");
            }

            // Parse response
            if (responseFormat == "text")
            {
                return Result<TranscriptionResult, string>.Success(
                    new TranscriptionResult(responseText.Trim()));
            }

            return ParseVerboseJsonResponse(responseText);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a string part to multipart content. Ownership is transferred to the parent content.
    /// </summary>
    private static void AddStringPart(MultipartFormDataContent parent, string value, string name)
    {
        StringContent? part = null;
        try
        {
            part = new StringContent(value);
            parent.Add(part, name);
            part = null; // Ownership transferred
        }
        finally
        {
            part?.Dispose();
        }
    }

    private static void AddStreamPart(MultipartFormDataContent parent, Stream stream, string extension, string fileName)
    {
        StreamContent? part = null;
        try
        {
            part = new StreamContent(stream);
            string mimeType = GetMimeType(extension);
            part.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            parent.Add(part, "file", fileName);
            part = null; // Ownership transferred
        }
        finally
        {
            part?.Dispose();
        }
    }

    private sealed class WhisperVerboseResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("segments")]
        public List<WhisperSegment>? Segments { get; set; }
    }

    private sealed class WhisperSegment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
