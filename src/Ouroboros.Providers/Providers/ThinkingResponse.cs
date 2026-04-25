using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.Providers;

/// <summary>
/// Represents a response from a model that includes both thinking/reasoning content and the final response.
/// Used by models that support extended thinking (Claude, DeepSeek R1, o1, etc.).
/// </summary>
/// <param name="Thinking">The thinking/reasoning content, if any. This is the model's internal reasoning process.</param>
/// <param name="Content">The final response content after thinking.</param>
/// <param name="ThinkingTokens">Estimated token count for thinking content (if available from API).</param>
/// <param name="ContentTokens">Estimated token count for content (if available from API).</param>
public sealed partial record ThinkingResponse(
    string? Thinking,
    string Content,
    int? ThinkingTokens = null,
    int? ContentTokens = null)
{
    /// <summary>
    /// Gets a value indicating whether returns true if this response contains thinking content.
    /// </summary>
    public bool HasThinking => !string.IsNullOrEmpty(Thinking);

    /// <summary>
    /// Combines thinking and content into a single formatted string.
    /// </summary>
    /// <param name="thinkingPrefix">Prefix for thinking section (default: "🤔 Thinking:\n").</param>
    /// <param name="contentPrefix">Prefix for content section (default: "\n\n📝 Response:\n").</param>
    /// <returns></returns>
    public string ToFormattedString(string thinkingPrefix = "🤔 Thinking:\n", string contentPrefix = "\n\n📝 Response:\n")
    {
        if (!HasThinking)
        {
            return Content;
        }

        return $"{thinkingPrefix}{Thinking}{contentPrefix}{Content}";
    }

    /// <summary>
    /// Creates a ThinkingResponse from raw text that may contain thinking tags.
    /// Supports &lt;think&gt;...&lt;/think&gt; format used by some models.
    /// </summary>
    /// <returns></returns>
    public static ThinkingResponse FromRawText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new ThinkingResponse(null, text ?? string.Empty);
        }

        // Try to extract <think>...</think> tags (used by DeepSeek R1, etc.)
        var thinkMatch = ThinkTagRegex().Match(text);
        if (thinkMatch.Success)
        {
            string thinking = thinkMatch.Groups[1].Value.Trim();
            string content = text.Replace(thinkMatch.Value, string.Empty).Trim();
            return new ThinkingResponse(thinking, content);
        }

        // Try <thinking>...</thinking> format
        var thinkingMatch = ThinkingTagRegex().Match(text);
        if (thinkingMatch.Success)
        {
            string thinking = thinkingMatch.Groups[1].Value.Trim();
            string content = text.Replace(thinkingMatch.Value, string.Empty).Trim();
            return new ThinkingResponse(thinking, content);
        }

        // No thinking tags found
        return new ThinkingResponse(null, text);
    }

    [GeneratedRegex(@"<think>(.*?)</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkTagRegex();

    [GeneratedRegex(@"<thinking>(.*?)</thinking>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkingTagRegex();
}
