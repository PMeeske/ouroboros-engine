// <copyright file="AzureNeuralTtsService.Streaming.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Microsoft.CognitiveServices.Speech;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Ouroboros.Providers.TextToSpeech;

public sealed partial class AzureNeuralTtsService
{
    // ========================================================================
    // IStreamingTtsService Implementation
    // ========================================================================

    /// <inheritdoc/>
    public bool IsSynthesizing => _isSynthesizing;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public IObservable<SpeechChunk> StreamSynthesis(
        IObservable<string> textStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        return textStream
            .BufferIntoSentences()
            .SelectMany(sentence => Observable.FromAsync<SpeechChunk?>(async token =>
            {
                var result = await SynthesizeChunkAsync(sentence, options, token).ConfigureAwait(false);
                return result.IsSuccess ? result.Value : null;
            }))
            .Where(chunk => chunk != null)
            .Select(chunk => chunk!);
    }

    /// <inheritdoc/>
    public IObservable<SpeechChunk> StreamSynthesisIncremental(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        return Observable.Create<SpeechChunk>(async (observer, token) =>
        {
            var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, token).Token;
            _currentSynthesisCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCt);

            try
            {
                _isSynthesizing = true;
                var sentences = StreamingTtsExtensions.SplitIntoSentences(text).ToList();

                for (int i = 0; i < sentences.Count; i++)
                {
                    if (_currentSynthesisCts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    var sentence = sentences[i];
                    var isLast = i == sentences.Count - 1;

                    var result = await SynthesizeChunkAsync(sentence, options, _currentSynthesisCts.Token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        var chunk = result.Value;
                        var finalChunk = new SpeechChunk(
                            chunk.AudioData,
                            chunk.Format,
                            chunk.DurationSeconds,
                            chunk.Text,
                            IsSentenceEnd: true,
                            IsComplete: isLast);
                        observer.OnNext(finalChunk);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Azure TTS] Chunk synthesis error: {result.Error}");
                    }
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnError(ex);
            }
            finally
            {
                _isSynthesizing = false;
            }
        });
    }

    /// <inheritdoc/>
    public async Task<Result<SpeechChunk, string>> SynthesizeChunkAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SpeechChunk, string>.Failure("Text cannot be empty");
        }

        if (_synthesizer == null) InitializeSynthesizer();

        try
        {
            _isSynthesizing = true;
            var rate      = options?.Speed ?? 1.0;
            var isWhisper = options?.IsWhisper ?? false;
            var ssml      = BuildSsml(text, isWhisper, cultureOverride: null, rate);

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml).ConfigureAwait(false);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                return Result<SpeechChunk, string>.Success(new SpeechChunk(
                    result.AudioData,
                    "audio/wav",
                    result.AudioDuration.TotalSeconds,
                    Text: text,
                    IsSentenceEnd: true,
                    IsComplete: false));
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                return Result<SpeechChunk, string>.Failure($"Speech synthesis canceled: {cancellation.ErrorDetails}");
            }

            return Result<SpeechChunk, string>.Failure("Speech synthesis failed");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<SpeechChunk, string>.Failure($"Azure TTS error: {ex.Message}");
        }
        finally
        {
            _isSynthesizing = false;
        }
    }

    /// <inheritdoc/>
    public void InterruptSynthesis()
    {
        try
        {
            _currentSynthesisCts?.Cancel();
            _isSynthesizing = false;
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }
}
