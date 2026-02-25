using System.Reactive.Linq;

namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Mock streaming transcription.
/// </summary>
public class MockStreamingTranscription : IStreamingTranscription
{
    public IObservable<TranscriptionResult> Results =>
        Observable.Empty<TranscriptionResult>();

    public IObservable<VoiceActivity> VoiceActivity =>
        Observable.Empty<VoiceActivity>();

    public string AccumulatedTranscript => "";
    public bool IsActive => false;

    public Task PushAudioAsync(byte[] audioData, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task EndAudioAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}