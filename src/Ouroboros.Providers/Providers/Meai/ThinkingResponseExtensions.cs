using Microsoft.Extensions.AI;

namespace Ouroboros.Providers.Meai;

/// <summary>
/// Extension methods for converting between MEAI types and Ouroboros ThinkingResponse.
/// </summary>
public static class ThinkingResponseExtensions
{
    /// <summary>
    /// Extracts a ThinkingResponse from a ChatResponse that may contain ThinkingAIContent.
    /// </summary>
    /// <returns></returns>
    public static ThinkingResponse ToThinkingResponse(this ChatResponse response)
    {
        string thinking = string.Empty;
        string content = string.Empty;

        foreach (var msg in response.Messages)
        {
            foreach (var c in msg.Contents)
            {
                if (c is ThinkingAIContent tc)
                {
                    thinking += tc.Text;
                }
                else if (c is TextContent text)
                {
                    content += text.Text;
                }
            }
        }

        return string.IsNullOrEmpty(thinking)
            ? new ThinkingResponse(null, content)
            : new ThinkingResponse(thinking, content);
    }

    /// <summary>
    /// Determines if a ChatResponseUpdate contains thinking content.
    /// </summary>
    /// <returns></returns>
    public static bool HasThinkingContent(this ChatResponseUpdate update)
    {
        return update.Contents.OfType<ThinkingAIContent>().Any();
    }

    /// <summary>
    /// Extracts the thinking text from a ChatResponseUpdate, if any.
    /// </summary>
    /// <returns></returns>
    public static string? GetThinkingText(this ChatResponseUpdate update)
    {
        var thinking = update.Contents.OfType<ThinkingAIContent>().FirstOrDefault();
        return thinking?.Text;
    }
}
