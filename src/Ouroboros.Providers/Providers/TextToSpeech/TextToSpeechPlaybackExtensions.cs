namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Extension methods for direct audio playback.
/// </summary>
public static class TextToSpeechPlaybackExtensions
{
    /// <summary>
    /// Synthesizes speech and plays it directly through the speakers.
    /// </summary>
    /// <param name="service">The TTS service.</param>
    /// <param name="text">The text to speak.</param>
    /// <param name="options">Optional TTS options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static async Task<Result<bool, string>> SpeakAsync(
        this ITextToSpeechService service,
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        Result<SpeechResult, string> synthesisResult = await service.SynthesizeAsync(text, options, ct);

        return synthesisResult.Match(
            speech => AudioPlayer.PlayAsync(speech, ct).GetAwaiter().GetResult(),
            error => Result<bool, string>.Failure(error));
    }
}