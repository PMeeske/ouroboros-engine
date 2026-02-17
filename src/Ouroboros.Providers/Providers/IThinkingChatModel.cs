namespace Ouroboros.Providers;

/// <summary>
/// Extended contract for models that support thinking/reasoning mode.
/// These models can return separate thinking content and response content.
/// Examples include Claude (with extended thinking), DeepSeek R1, and o1 models.
/// </summary>
public interface IThinkingChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel
{
    /// <summary>
    /// Generates a response with separate thinking and content.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ThinkingResponse containing both thinking and content.</returns>
    Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default);
}