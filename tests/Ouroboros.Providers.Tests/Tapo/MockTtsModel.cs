using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Mock TTS model for testing.
/// </summary>
internal sealed class MockTtsModel : ITtsModel
{
    public string ModelName => "MockTTS";
    public bool SupportsStreaming => false;
    public bool SupportsEmotions => false;

    public Task<Result<IReadOnlyList<VoiceInfo>, string>> GetVoicesAsync(
        string? language = null,
        CancellationToken ct = default)
    {
        var voices = new List<VoiceInfo>
        {
            new("default", "Default Voice", "en-US", "neutral", [])
        };
        return Task.FromResult(Result<IReadOnlyList<VoiceInfo>, string>.Success(voices));
    }

    public Task<Result<SynthesizedSpeech, string>> SynthesizeAsync(
        string text,
        VoiceConfig? config = null,
        CancellationToken ct = default)
    {
        var speech = new SynthesizedSpeech(
            text,
            new byte[100],
            "wav",
            16000,
            TimeSpan.FromSeconds(text.Length * 0.1),
            DateTime.UtcNow);
        return Task.FromResult(Result<SynthesizedSpeech, string>.Success(speech));
    }

    public IObservable<byte[]> SynthesizeStreaming(
        string text,
        VoiceConfig? config = null,
        CancellationToken ct = default)
    {
        return System.Reactive.Linq.Observable.Empty<byte[]>();
    }
}