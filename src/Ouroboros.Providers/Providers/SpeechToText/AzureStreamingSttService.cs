// <copyright file="AzureStreamingSttService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Azure Speech SDK implementation of <see cref="IStreamingSttService"/>.
/// Uses continuous recognition via <see cref="AudioInputStream.CreatePushStream"/>
/// instead of the simpler RecognizeOnceAsync, enabling real-time streaming STT
/// with interim results and barge-in support.
/// </summary>
public sealed class AzureStreamingSttService : IStreamingSttService, IDisposable
{
    private readonly SpeechConfig _config;
    private bool _disposed;

    /// <inheritdoc/>
    public string ProviderName => "Azure Speech (Streaming)";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => [".wav", ".pcm"];

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 500 * 1024 * 1024;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public bool SupportsVoiceActivityDetection => true;

    /// <summary>
    /// Creates a new Azure streaming STT service.
    /// </summary>
    /// <param name="subscriptionKey">Azure Speech subscription key.</param>
    /// <param name="region">Azure Speech region.</param>
    /// <param name="language">Speech recognition language (e.g. "en-US").</param>
    public AzureStreamingSttService(string subscriptionKey, string region, string? language = null)
    {
        _config = SpeechConfig.FromSubscription(subscriptionKey, region);
        _config.SpeechRecognitionLanguage = language ?? "en-US";
        _config.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");
    }

    /// <inheritdoc/>
    public IObservable<TranscriptionEvent> StreamTranscription(
        IObservable<AudioChunk> audioStream,
        StreamingTranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        return Observable.Create<TranscriptionEvent>(async (observer, disposalCt) =>
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, disposalCt);
            var disposables = new CompositeDisposable();
            disposables.Add(linkedCts);

            try
            {
                using var pushStream = AudioInputStream.CreatePushStream(
                    AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
                using var audioConfig = AudioConfig.FromStreamInput(pushStream);

                if (options?.Language != null)
                {
                    _config.SpeechRecognitionLanguage = options.Language;
                }

                using var recognizer = new SpeechRecognizer(_config, audioConfig);

                var recognitionStarted = new TaskCompletionSource<bool>();

                // Interim results (Recognizing event)
                recognizer.Recognizing += (_, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Result.Text)) return;
                    observer.OnNext(new TranscriptionEvent(
                        e.Result.Text,
                        IsFinal: false,
                        Confidence: 0.5,
                        Offset: TimeSpan.FromTicks(e.Result.OffsetInTicks),
                        Duration: e.Result.Duration));
                };

                // Final results (Recognized event)
                recognizer.Recognized += (_, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech &&
                        !string.IsNullOrWhiteSpace(e.Result.Text))
                    {
                        observer.OnNext(new TranscriptionEvent(
                            e.Result.Text,
                            IsFinal: true,
                            Confidence: 1.0,
                            Offset: TimeSpan.FromTicks(e.Result.OffsetInTicks),
                            Duration: e.Result.Duration));
                    }
                };

                recognizer.Canceled += (_, e) =>
                {
                    if (e.Reason == CancellationReason.Error)
                    {
                        observer.OnError(new InvalidOperationException(
                            $"Azure Speech error [{e.ErrorCode}]: {e.ErrorDetails}"));
                    }
                    else
                    {
                        observer.OnCompleted();
                    }
                };

                recognizer.SessionStopped += (_, _) => observer.OnCompleted();

                await recognizer.StartContinuousRecognitionAsync();
                recognitionStarted.TrySetResult(true);

                // Pump audio chunks into the push stream
                var audioSubscription = audioStream.Subscribe(
                    chunk =>
                    {
                        if (!linkedCts.Token.IsCancellationRequested)
                        {
                            pushStream.Write(chunk.Data, chunk.Data.Length);
                            if (chunk.IsFinal)
                            {
                                pushStream.Close();
                            }
                        }
                    },
                    ex =>
                    {
                        pushStream.Close();
                        observer.OnError(ex);
                    },
                    () => pushStream.Close());

                disposables.Add(audioSubscription);

                // Wait for cancellation
                try
                {
                    await Task.Delay(Timeout.Infinite, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }

                await recognizer.StopContinuousRecognitionAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
            finally
            {
                disposables.Dispose();
            }
        });
    }

    /// <inheritdoc/>
    public IObservable<VoiceActivityEvent> DetectVoiceActivity(
        IObservable<AudioChunk> audioStream,
        CancellationToken ct = default)
    {
        // Derive VAD from the transcription stream
        return StreamTranscription(audioStream, ct: ct)
            .Select(e => new VoiceActivityEvent(
                e.IsFinal ? VoiceActivity.SpeechEnd : VoiceActivity.SpeechStart,
                DateTimeOffset.UtcNow));
    }

    /// <inheritdoc/>
    public async Task<IStreamingTranscriptionSession> StartStreamingSessionAsync(
        StreamingTranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        var session = new AzureStreamingSession(_config, options);
        await session.InitializeAsync(ct);
        return session;
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath, TranscriptionOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            using var audioConfig = AudioConfig.FromWavFileInput(filePath);
            using var recognizer = new SpeechRecognizer(_config, audioConfig);
            var result = await recognizer.RecognizeOnceAsync();

            return result.Reason == ResultReason.RecognizedSpeech
                ? new TranscriptionResult(result.Text, _config.SpeechRecognitionLanguage, result.Duration.TotalSeconds)
                : Result<TranscriptionResult, string>.Failure($"Recognition failed: {result.Reason}");
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream, string fileName, TranscriptionOptions? options = null, CancellationToken ct = default)
    {
        var bytes = new byte[audioStream.Length];
        _ = await audioStream.ReadAsync(bytes, ct);
        return await TranscribeBytesAsync(bytes, fileName, options, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData, string fileName, TranscriptionOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            using var pushStream = AudioInputStream.CreatePushStream(
                AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
            pushStream.Write(audioData, audioData.Length);
            pushStream.Close();

            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var recognizer = new SpeechRecognizer(_config, audioConfig);
            var result = await recognizer.RecognizeOnceAsync();

            return result.Reason == ResultReason.RecognizedSpeech
                ? new TranscriptionResult(result.Text, _config.SpeechRecognitionLanguage, result.Duration.TotalSeconds)
                : Result<TranscriptionResult, string>.Failure($"Recognition failed: {result.Reason}");
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath, TranscriptionOptions? options = null, CancellationToken ct = default)
        => TranscribeFileAsync(filePath, options, ct);

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrEmpty(_config.SubscriptionKey));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    // ════════════════════════════════════════════════════════════════
    // Streaming Session
    // ════════════════════════════════════════════════════════════════

    private sealed class AzureStreamingSession : IStreamingTranscriptionSession
    {
        private readonly SpeechConfig _config;
        private readonly StreamingTranscriptionOptions? _options;
        private readonly Subject<TranscriptionEvent> _results = new();
        private readonly Subject<VoiceActivityEvent> _voiceActivity = new();
        private readonly StringBuilder _accumulated = new();

        private PushAudioInputStream? _pushStream;
        private AudioConfig? _audioConfig;
        private SpeechRecognizer? _recognizer;
        private bool _isActive;

        public AzureStreamingSession(SpeechConfig config, StreamingTranscriptionOptions? options)
        {
            _config = config;
            _options = options;
        }

        public IObservable<TranscriptionEvent> Results => _results.AsObservable();
        public IObservable<VoiceActivityEvent> VoiceActivity => _voiceActivity.AsObservable();
        public string AccumulatedText => _accumulated.ToString();
        public bool IsActive => _isActive;

        public async Task InitializeAsync(CancellationToken ct)
        {
            _pushStream = AudioInputStream.CreatePushStream(
                AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
            _audioConfig = AudioConfig.FromStreamInput(_pushStream);

            if (_options?.Language != null)
            {
                _config.SpeechRecognitionLanguage = _options.Language;
            }

            _recognizer = new SpeechRecognizer(_config, _audioConfig);

            _recognizer.Recognizing += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Result.Text)) return;
                _results.OnNext(new TranscriptionEvent(
                    e.Result.Text, false, 0.5,
                    TimeSpan.FromTicks(e.Result.OffsetInTicks),
                    e.Result.Duration));
                _voiceActivity.OnNext(new VoiceActivityEvent(
                    SpeechToText.VoiceActivity.SpeechStart, DateTimeOffset.UtcNow));
            };

            _recognizer.Recognized += (_, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech &&
                    !string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    _accumulated.Append(e.Result.Text).Append(' ');
                    _results.OnNext(new TranscriptionEvent(
                        e.Result.Text, true, 1.0,
                        TimeSpan.FromTicks(e.Result.OffsetInTicks),
                        e.Result.Duration));
                    _voiceActivity.OnNext(new VoiceActivityEvent(
                        SpeechToText.VoiceActivity.SpeechEnd, DateTimeOffset.UtcNow));
                }
            };

            _recognizer.Canceled += (_, e) =>
            {
                _isActive = false;
                if (e.Reason == CancellationReason.Error)
                {
                    _results.OnError(new InvalidOperationException(e.ErrorDetails));
                }
            };

            _recognizer.SessionStopped += (_, _) =>
            {
                _isActive = false;
                _results.OnCompleted();
                _voiceActivity.OnCompleted();
            };

            await _recognizer.StartContinuousRecognitionAsync();
            _isActive = true;
        }

        public Task PushAudioAsync(AudioChunk chunk, CancellationToken ct = default)
        {
            if (_pushStream != null && _isActive)
            {
                _pushStream.Write(chunk.Data, chunk.Data.Length);
            }

            return Task.CompletedTask;
        }

        public async Task EndAudioAsync(CancellationToken ct = default)
        {
            _pushStream?.Close();
            if (_recognizer != null)
            {
                await _recognizer.StopContinuousRecognitionAsync();
            }

            _isActive = false;
        }

        public void Reset()
        {
            _accumulated.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            _isActive = false;
            _pushStream?.Close();

            if (_recognizer != null)
            {
                try { await _recognizer.StopContinuousRecognitionAsync(); }
                catch { /* Best effort */ }
                _recognizer.Dispose();
            }

            _audioConfig?.Dispose();
            _results.Dispose();
            _voiceActivity.Dispose();
        }
    }
}
