using R3;

namespace Ouroboros.Providers;

/// <summary>
/// Extended contract for models that support streaming responses.
/// </summary>
[Obsolete("Use IOuroborosChatClient with GetStreamingResponseAsync instead. Will be removed in v3.")]
public interface IStreamingChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel
{
    Observable<string> StreamReasoningContent(string prompt, CancellationToken ct = default);
}
