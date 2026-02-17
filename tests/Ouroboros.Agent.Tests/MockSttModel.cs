namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Mock STT model for testing.
/// </summary>
public class MockSttModel : ISttModel
{
    public string ModelName => "MockSTT";
    public bool SupportsStreaming => false;

    public Task<Result<TranscriptionResult, string>> TranscribeAsync(
        string audioFilePath,
        string? language = null,
        CancellationToken ct = default)
    {
        var result = new TranscriptionResult(
            "Mock transcription from file",
            0.95,
            language ?? "en-US",
            true,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            null);
        return Task.FromResult(Result<TranscriptionResult, string>.Success(result));
    }

    public Task<Result<TranscriptionResult, string>> TranscribeAsync(
        byte[] audioData,
        string format,
        int sampleRate,
        string? language = null,
        CancellationToken ct = default)
    {
        var result = new TranscriptionResult(
            "Mock transcription from bytes",
            0.95,
            language ?? "en-US",
            true,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            null);
        return Task.FromResult(Result<TranscriptionResult, string>.Success(result));
    }

    public IStreamingTranscription CreateStreamingSession(string? language = null)
    {
        return new MockStreamingTranscription();
    }
}