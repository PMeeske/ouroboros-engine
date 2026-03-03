using Microsoft.Extensions.AI;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Custom AIContent type for thinking/reasoning tokens in streaming responses.
/// Distinguishes thinking chunks from regular content in ChatResponseUpdate.
/// </summary>
public sealed class ThinkingAIContent : AIContent
{
    public ThinkingAIContent(string text) { Text = text; }
    public string Text { get; }
    public override string ToString() => Text;
}
