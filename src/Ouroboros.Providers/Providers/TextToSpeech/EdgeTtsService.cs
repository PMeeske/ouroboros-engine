// Copyright (c) Ouroboros. All rights reserved.

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Stub implementation of Edge TTS service.
/// </summary>
public sealed class EdgeTtsService
{
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<Result<SpeechResult, string>> SynthesizeAsync(
        string text, string? voiceId = null, CancellationToken ct = default)
        => Task.FromResult(Result<SpeechResult, string>.Failure("EdgeTTS not available"));
}
