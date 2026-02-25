using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Providers;

/// <summary>
/// Extension methods for registering text-to-speech tools.
/// </summary>
public static class TextToSpeechToolExtensions
{
    /// <summary>
    /// Adds text-to-speech tool to the registry using OpenAI TTS.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    /// <param name="model">TTS model: "tts-1" or "tts-1-hd".</param>
    /// <returns>A new registry with the text-to-speech tool added.</returns>
    public static ToolRegistry WithTextToSpeech(this ToolRegistry registry, string? apiKey = null, string model = "tts-1")
    {
        string? key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
        }

        return registry.WithTool(TextToSpeechTool.CreateWithOpenAi(key, model));
    }

    /// <summary>
    /// Adds text-to-speech tool to the registry with a custom service.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="service">The text-to-speech service to use.</param>
    /// <returns>A new registry with the text-to-speech tool added.</returns>
    public static ToolRegistry WithTextToSpeech(this ToolRegistry registry, ITextToSpeechService service)
    {
        return registry.WithTool(new TextToSpeechTool(service));
    }

    /// <summary>
    /// Adds both speech-to-text and text-to-speech tools for bidirectional audio support.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    /// <returns>A new registry with both audio tools added.</returns>
    public static ToolRegistry WithBidirectionalSpeech(this ToolRegistry registry, string? apiKey = null)
    {
        return registry
            .WithSpeechToText(apiKey)
            .WithTextToSpeech(apiKey);
    }
}