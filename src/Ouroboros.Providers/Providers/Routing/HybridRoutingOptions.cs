namespace Ouroboros.Providers.Routing;

/// <summary>
/// Configuration options for hybrid routing with Ollama-based models.
/// </summary>
/// <param name="Enabled">Whether hybrid routing is enabled (default: true).</param>
/// <param name="DefaultOllamaModel">Default Ollama model for simple tasks (default: llama3.1:8b).</param>
/// <param name="ReasoningOllamaModel">Ollama model for reasoning tasks (default: deepseek-r1:32b).</param>
/// <param name="UseDeepSeekForPlanning">Whether to use DeepSeek for planning tasks (default: true).</param>
/// <param name="FallbackToLocal">Whether to fallback to local models when cloud unavailable (default: true).</param>
public record HybridRoutingOptions(
    bool Enabled = true,
    string DefaultOllamaModel = "llama3.1:8b",
    string ReasoningOllamaModel = "deepseek-r1:32b",
    bool UseDeepSeekForPlanning = true,
    bool FallbackToLocal = true);