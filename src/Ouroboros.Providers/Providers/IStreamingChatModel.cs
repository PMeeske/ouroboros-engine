namespace Ouroboros.Providers;

/// <summary>
/// Extended contract for models that support streaming responses.
/// </summary>
public interface IStreamingChatModel : Ouroboros.Abstractions.Core.IChatCompletionModel
{
    IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default);
}