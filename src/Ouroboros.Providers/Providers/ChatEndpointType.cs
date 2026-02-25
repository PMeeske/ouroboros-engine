namespace Ouroboros.Providers;

/// <summary>
/// Represents the type of remote chat endpoint being used.
/// </summary>
public enum ChatEndpointType
{
    /// <summary>
    /// Auto-detect endpoint type based on URL or use OpenAI-compatible as default
    /// </summary>
    Auto,
    /// <summary>
    /// OpenAI-compatible API (original behavior)
    /// </summary>
    OpenAiCompatible,
    /// <summary>
    /// Native OpenAI API (api.openai.com)
    /// </summary>
    OpenAI,
    /// <summary>
    /// Ollama Cloud API format
    /// </summary>
    OllamaCloud,
    /// <summary>
    /// Local Ollama instance
    /// </summary>
    OllamaLocal,
    /// <summary>
    /// LiteLLM proxy with OpenAI-compatible chat completions
    /// </summary>
    LiteLLM,
    /// <summary>
    /// GitHub Models API with OpenAI-compatible chat completions
    /// </summary>
    GitHubModels,
    /// <summary>
    /// Anthropic Claude API (native Messages API format)
    /// </summary>
    Anthropic,
    /// <summary>
    /// Azure OpenAI Service
    /// </summary>
    AzureOpenAI,
    /// <summary>
    /// Google AI (Gemini) API
    /// </summary>
    Google,
    /// <summary>
    /// Mistral AI API
    /// </summary>
    Mistral,
    /// <summary>
    /// DeepSeek API
    /// </summary>
    DeepSeek,
    /// <summary>
    /// Cohere API
    /// </summary>
    Cohere,
    /// <summary>
    /// Groq API (fast inference)
    /// </summary>
    Groq,
    /// <summary>
    /// Together AI API
    /// </summary>
    Together,
    /// <summary>
    /// Fireworks AI API
    /// </summary>
    Fireworks,
    /// <summary>
    /// Perplexity AI API
    /// </summary>
    Perplexity,
    /// <summary>
    /// Replicate API
    /// </summary>
    Replicate,
    /// <summary>
    /// HuggingFace Inference API
    /// </summary>
    HuggingFace
}