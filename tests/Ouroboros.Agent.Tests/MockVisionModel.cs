namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Mock vision model for testing.
/// </summary>
public class MockVisionModel : IVisionModel
{
    public string ModelName => "MockVision";
    public bool SupportsStreaming => false;

    public Task<Result<VisionAnalysisResult, string>> AnalyzeImageAsync(
        byte[] imageData,
        string format,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new VisionAnalysisResult(
            "Mock scene description",
            Array.Empty<DetectedObject>(),
            Array.Empty<DetectedFace>(),
            "office",
            ["gray", "white"],
            null,
            0.9,
            50);
        return Task.FromResult(Result<VisionAnalysisResult, string>.Success(result));
    }

    public Task<Result<VisionAnalysisResult, string>> AnalyzeImageFileAsync(
        string filePath,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new VisionAnalysisResult(
            "Mock scene from file",
            Array.Empty<DetectedObject>(),
            Array.Empty<DetectedFace>(),
            "office",
            ["gray", "white"],
            null,
            0.9,
            100);
        return Task.FromResult(Result<VisionAnalysisResult, string>.Success(result));
    }

    public Task<Result<string, string>> AnswerQuestionAsync(
        byte[] imageData,
        string format,
        string question,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Success($"Mock answer to: {question}"));
    }

    public Task<Result<IReadOnlyList<DetectedObject>, string>> DetectObjectsAsync(
        byte[] imageData,
        string format,
        int maxObjects = 20,
        CancellationToken ct = default)
    {
        var objects = new List<DetectedObject>
        {
            new("mock-object", 0.9, (0.1, 0.1, 0.2, 0.2), null)
        };
        return Task.FromResult(Result<IReadOnlyList<DetectedObject>, string>.Success(objects));
    }

    public Task<Result<IReadOnlyList<DetectedFace>, string>> DetectFacesAsync(
        byte[] imageData,
        string format,
        bool analyzeEmotion = true,
        CancellationToken ct = default)
    {
        var faces = new List<DetectedFace>();
        return Task.FromResult(Result<IReadOnlyList<DetectedFace>, string>.Success(faces));
    }
}