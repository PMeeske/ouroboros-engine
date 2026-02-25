namespace Ouroboros.Providers;

/// <summary>
/// HTTP client for LiteLLM proxy endpoints that support OpenAI-compatible chat completions API.
/// Uses standard /v1/chat/completions endpoint with messages format.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class LiteLLMChatModel : OpenAiCompatibleChatModelBase
{
    public LiteLLMChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
        : base(endpoint, apiKey, model, "LiteLLMChatModel", settings, costTracker)
    {
    }
}