namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Configuration for a specialized model including Ollama Cloud settings.
/// </summary>
/// <param name="Role">The role this configuration is for.</param>
/// <param name="OllamaModel">The Ollama model identifier (e.g., "llama3.1:70b").</param>
/// <param name="Endpoint">Optional custom endpoint (defaults to Ollama Cloud).</param>
/// <param name="Capabilities">Capabilities this model provides.</param>
/// <param name="Priority">Selection priority.</param>
/// <param name="MaxTokens">Maximum context length.</param>
/// <param name="Temperature">Temperature setting for generation.</param>
public sealed record SpecializedModelConfig(
    SpecializedRole Role,
    string OllamaModel,
    string? Endpoint = null,
    string[]? Capabilities = null,
    double Priority = 1.0,
    int MaxTokens = 4096,
    double Temperature = 0.7);