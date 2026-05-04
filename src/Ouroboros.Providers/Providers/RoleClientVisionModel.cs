// <copyright file="RoleClientVisionModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Chat;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Providers;

/// <summary>
/// <see cref="IVisionModel"/> implementation that delegates to an
/// <see cref="IVisionRoleClient"/>. Lets the rich avatar-perception
/// surface (FrameAssessmentService, MirrorRecognitionService, etc.)
/// run on the role-bound vision client (Phi-3.5-vision in
/// <c>--mode hermes-*</c>, or any future ONNX/cloud VLM wired through
/// the role registration) without touching every consumer.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the prompt + JSON-parsing logic of <see cref="OllamaVisionModel"/>
/// — the parsers are intentionally permissive because VLM output can
/// drift between strict JSON arrays and natural-language descriptions
/// depending on prompt phrasing, model version, and sampling temperature.
/// </para>
/// <para>
/// Disposal: this adapter does NOT dispose the wrapped role client —
/// DI container owns the underlying singleton's lifetime.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Vision-role client failure modes span ORT-GenAI (OnnxRuntimeGenAIException), HTTP (HttpRequestException), I/O (temp-file write for Phi3v image preprocessing), and JSON parsing — any of these must surface as Result.Failure rather than crash the avatar perception pipeline.")]
public sealed class RoleClientVisionModel : IVisionModel
{
    private const string DefaultModelName = "vision-role-client";

    private readonly IVisionRoleClient _client;
    private readonly string _modelName;
    private readonly ILogger<RoleClientVisionModel>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleClientVisionModel"/> class.
    /// </summary>
    /// <param name="client">Backing role-typed vision client (e.g. Phi-3.5-vision).</param>
    /// <param name="modelName">Friendly identifier surfaced via <see cref="ModelName"/>.</param>
    /// <param name="logger">Optional structured logger.</param>
    public RoleClientVisionModel(
        IVisionRoleClient client,
        string modelName = DefaultModelName,
        ILogger<RoleClientVisionModel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _modelName = string.IsNullOrWhiteSpace(modelName) ? DefaultModelName : modelName;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ModelName => _modelName;

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <inheritdoc/>
    public async Task<Result<VisionAnalysisResult, string>> AnalyzeImageAsync(
        byte[] imageData,
        string format,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        options ??= new VisionAnalysisOptions();
        string prompt = BuildAnalysisPrompt(options);

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            string response = await CallVisionAsync(imageData, format, prompt, ct).ConfigureAwait(false);
            sw.Stop();

            _logger?.LogDebug("[RoleClientVision] Analysis completed in {ElapsedMs}ms via {Model}", sw.ElapsedMilliseconds, _modelName);
            VisionAnalysisResult result = ParseAnalysisResponse(response, sw.ElapsedMilliseconds, options);
            return Result<VisionAnalysisResult, string>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RoleClientVision] Analysis failed via {Model}", _modelName);
            return Result<VisionAnalysisResult, string>.Failure($"Vision analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<VisionAnalysisResult, string>> AnalyzeImageFileAsync(
        string filePath,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            return Result<VisionAnalysisResult, string>.Failure($"File not found: {filePath}");
        }

        byte[] imageData = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        string ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        string mime = ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "bmp" => "image/bmp",
            "gif" => "image/gif",
            "webp" => "image/webp",
            _ => "image/jpeg",
        };

        return await AnalyzeImageAsync(imageData, mime, options, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> AnswerQuestionAsync(
        byte[] imageData,
        string format,
        string question,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        try
        {
            string response = await CallVisionAsync(imageData, format, question, ct).ConfigureAwait(false);
            return Result<string, string>.Success(response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RoleClientVision] Q&A failed via {Model}", _modelName);
            return Result<string, string>.Failure($"Vision Q&A failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DetectedObject>, string>> DetectObjectsAsync(
        byte[] imageData,
        string format,
        int maxObjects = 20,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        string prompt = $"List all objects you can see in this image (up to {maxObjects}). " +
                        "For each object, provide: label, confidence (0-1), and approximate bounding box " +
                        "as x,y,width,height (0-1 normalized). Format as JSON array: " +
                        "[{\"label\":\"...\",\"confidence\":0.9,\"x\":0.1,\"y\":0.2,\"w\":0.3,\"h\":0.4}]";

        try
        {
            string response = await CallVisionAsync(imageData, format, prompt, ct).ConfigureAwait(false);
            List<DetectedObject> objects = ParseDetectedObjects(response, maxObjects);
            return Result<IReadOnlyList<DetectedObject>, string>.Success(objects);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RoleClientVision] Object detection failed via {Model}", _modelName);
            return Result<IReadOnlyList<DetectedObject>, string>.Failure($"Object detection failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DetectedFace>, string>> DetectFacesAsync(
        byte[] imageData,
        string format,
        bool analyzeEmotion = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        string emotionPart = analyzeEmotion ? " and their emotion (happy, sad, neutral, surprised, angry, fearful, disgusted)" : string.Empty;
        string prompt = $"Detect all human faces in this image. For each face, describe their approximate location" +
                        $"{emotionPart} and estimated age. Format as JSON array: " +
                        "[{\"emotion\":\"happy\",\"age\":30,\"x\":0.1,\"y\":0.2,\"w\":0.3,\"h\":0.4}]";

        try
        {
            string response = await CallVisionAsync(imageData, format, prompt, ct).ConfigureAwait(false);
            List<DetectedFace> faces = ParseDetectedFaces(response, analyzeEmotion);
            return Result<IReadOnlyList<DetectedFace>, string>.Success(faces);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[RoleClientVision] Face detection failed via {Model}", _modelName);
            return Result<IReadOnlyList<DetectedFace>, string>.Failure($"Face detection failed: {ex.Message}");
        }
    }

    private async Task<string> CallVisionAsync(byte[] imageData, string format, string prompt, CancellationToken ct)
    {
        string mime = format.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? format : $"image/{format}";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User,
            [
                new DataContent(imageData, mime),
                new TextContent(prompt),
            ]),
        };

        ChatResponse response = await _client.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
        return response.Text ?? string.Empty;
    }

    private static string BuildAnalysisPrompt(VisionAnalysisOptions options)
    {
        StringBuilder sb = new();
        if (options.IncludeDescription) sb.Append("Describe what you see in this image in detail. ");
        if (options.DetectObjects) sb.Append($"Identify and list all distinct objects (up to {options.MaxObjects}). ");
        if (options.DetectFaces) sb.Append("Note any human faces, their approximate emotions, and estimated ages. ");
        if (options.ClassifyScene) sb.Append("Classify the scene type (indoor, outdoor, office, nature, street, etc.). ");
        if (options.ExtractText) sb.Append("Extract any visible text (OCR). ");
        if (options.AnalyzeColors) sb.Append("Note the dominant colors in the scene. ");
        if (sb.Length == 0) sb.Append("Describe what you see in this image.");
        return sb.ToString().Trim();
    }

    private static VisionAnalysisResult ParseAnalysisResponse(
        string response,
        long processingTimeMs,
        VisionAnalysisOptions options)
    {
        List<DetectedObject> objects = options.DetectObjects
            ? ParseDetectedObjects(response, options.MaxObjects)
            : new List<DetectedObject>();
        List<DetectedFace> faces = options.DetectFaces
            ? ParseDetectedFaces(response, true)
            : new List<DetectedFace>();
        string? sceneType = options.ClassifyScene ? ExtractSceneType(response) : null;
        List<string>? colors = options.AnalyzeColors ? ExtractColors(response) : null;
        string? text = options.ExtractText ? ExtractOcrText(response) : null;

        return new VisionAnalysisResult(
            Description: response,
            Objects: objects,
            Faces: faces,
            SceneType: sceneType,
            DominantColors: colors,
            Text: text,
            Confidence: 0.85,
            ProcessingTimeMs: processingTimeMs);
    }

    private static List<DetectedObject> ParseDetectedObjects(string response, int maxObjects)
    {
        List<DetectedObject> objects = new();
        try
        {
            int jsonStart = response.IndexOf('[', StringComparison.Ordinal);
            int jsonEnd = response.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonStr = response[jsonStart..(jsonEnd + 1)];
                JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                foreach (JsonElement item in jsonArray.EnumerateArray())
                {
                    string label = item.TryGetProperty("label", out JsonElement lblEl) ? lblEl.GetString() ?? "unknown" : "unknown";
                    double confidence = item.TryGetProperty("confidence", out JsonElement conf) ? conf.GetDouble() : 0.7;
                    double x = item.TryGetProperty("x", out JsonElement xVal) ? xVal.GetDouble() : 0;
                    double y = item.TryGetProperty("y", out JsonElement yVal) ? yVal.GetDouble() : 0;
                    double w = item.TryGetProperty("w", out JsonElement wVal) ? wVal.GetDouble() : 0;
                    double h = item.TryGetProperty("h", out JsonElement hVal) ? hVal.GetDouble() : 0;

                    objects.Add(new DetectedObject(label, confidence, (x, y, w, h), null));
                    if (objects.Count >= maxObjects) break;
                }
            }
        }
        catch (JsonException) { /* VLM may emit prose instead of JSON; degrade silently */ }

        return objects;
    }

    private static List<DetectedFace> ParseDetectedFaces(string response, bool analyzeEmotion)
    {
        List<DetectedFace> faces = new();
        try
        {
            int jsonStart = response.IndexOf('[', StringComparison.Ordinal);
            int jsonEnd = response.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonStr = response[jsonStart..(jsonEnd + 1)];
                JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                foreach (JsonElement item in jsonArray.EnumerateArray())
                {
                    string? emotion = analyzeEmotion && item.TryGetProperty("emotion", out JsonElement emo)
                        ? emo.GetString() : null;
                    int? age = item.TryGetProperty("age", out JsonElement ageVal) ? ageVal.GetInt32() : null;
                    double x = item.TryGetProperty("x", out JsonElement xVal) ? xVal.GetDouble() : 0;
                    double y = item.TryGetProperty("y", out JsonElement yVal) ? yVal.GetDouble() : 0;
                    double w = item.TryGetProperty("w", out JsonElement wVal) ? wVal.GetDouble() : 0;
                    double h = item.TryGetProperty("h", out JsonElement hVal) ? hVal.GetDouble() : 0;

                    faces.Add(new DetectedFace(
                        FaceId: Guid.NewGuid().ToString("N")[..8],
                        Confidence: 0.8,
                        BoundingBox: (x, y, w, h),
                        Emotion: emotion,
                        Age: age,
                        IsKnown: false,
                        PersonId: null));
                }
            }
        }
        catch (JsonException) { /* prose response — return empty */ }

        return faces;
    }

    private static string? ExtractSceneType(string response)
    {
        string lower = response.ToLowerInvariant();
        string[] sceneTypes = ["outdoor", "indoor", "office", "nature", "street", "kitchen", "bedroom",
            "living room", "bathroom", "garden", "park", "beach", "forest", "city", "rural",
            "restaurant", "store", "classroom", "laboratory", "warehouse", "parking"];
        return sceneTypes.FirstOrDefault(s => lower.Contains(s, StringComparison.Ordinal));
    }

    private static List<string>? ExtractColors(string response)
    {
        string lower = response.ToLowerInvariant();
        string[] colorNames = ["red", "blue", "green", "yellow", "orange", "purple", "pink",
            "white", "black", "gray", "brown", "beige", "teal", "cyan", "magenta"];
        List<string> found = colorNames.Where(c => lower.Contains(c, StringComparison.Ordinal)).ToList();
        return found.Count > 0 ? found : null;
    }

    private static string? ExtractOcrText(string response)
    {
        string[] markers = ["reads:", "says:", "text:", "written:", "\""];
        foreach (string marker in markers)
        {
            int idx = response.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string after = response[(idx + marker.Length)..].Trim();
                int endIdx = after.IndexOfAny(['.', '\n', '"']);
                if (endIdx > 0) return after[..endIdx].Trim();
                if (after.Length > 0 && after.Length < 200) return after.Trim();
            }
        }

        return null;
    }
}
