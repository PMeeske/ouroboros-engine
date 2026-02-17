namespace Ouroboros.Providers;

/// <summary>
/// Extended contract for models that support streaming thinking/reasoning mode.
/// </summary>
public interface IStreamingThinkingChatModel : IThinkingChatModel, IStreamingChatModel
{
    /// <summary>
    /// Streams the thinking and content separately.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An observable that emits (isThinking, chunk) tuples.</returns>
    IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default);
}