using Ouroboros.Providers.SpeechToText;

namespace Ouroboros.Providers;

/// <summary>
/// Extension methods for registering speech-to-text tools.
/// </summary>
public static class SpeechToTextToolExtensions
{
    /// <summary>
    /// Adds speech-to-text tool to the registry using OpenAI Whisper.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    /// <returns>A new registry with the speech-to-text tool added.</returns>
    public static ToolRegistry WithSpeechToText(this ToolRegistry registry, string? apiKey = null)
    {
        string? key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
        }

        return registry.WithTool(SpeechToTextTool.CreateWithWhisper(key));
    }

    /// <summary>
    /// Adds speech-to-text tool to the registry using local Whisper.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="modelSize">Whisper model size: tiny, base, small, medium, large.</param>
    /// <returns>A new registry with the local speech-to-text tool added.</returns>
    public static ToolRegistry WithLocalSpeechToText(this ToolRegistry registry, string modelSize = "small")
    {
        return registry.WithTool(SpeechToTextTool.CreateWithLocalWhisper(modelSize));
    }

    /// <summary>
    /// Adds speech-to-text tool to the registry with a custom service.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="service">The speech-to-text service to use.</param>
    /// <returns>A new registry with the speech-to-text tool added.</returns>
    public static ToolRegistry WithSpeechToText(this ToolRegistry registry, ISpeechToTextService service)
    {
        return registry.WithTool(new SpeechToTextTool(service));
    }
}