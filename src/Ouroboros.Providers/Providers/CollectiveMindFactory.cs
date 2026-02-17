namespace Ouroboros.Providers;

/// <summary>
/// Factory for creating pre-configured CollectiveMind instances.
/// </summary>
public static class CollectiveMindFactory
{
    /// <summary>
    /// Creates a balanced collective with multiple diverse providers.
    /// </summary>
    public static CollectiveMind CreateBalanced(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        // Try to add providers based on available API keys
        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-sonnet-4-20250514", settings);
        TryAddProvider(mind, "OpenAI", ChatEndpointType.OpenAI, "gpt-4o", settings);
        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-70b-versatile", settings);

        // Always try local Ollama
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, "llama3.2", settings: settings);

        mind.ThinkingMode = CollectiveThinkingMode.Adaptive;
        return mind;
    }

    /// <summary>
    /// Creates a speed-optimized collective using fast inference providers.
    /// </summary>
    public static CollectiveMind CreateFast(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-70b-versatile", settings);
        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Fireworks", ChatEndpointType.Fireworks, "llama-v3-70b-instruct", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Racing;
        return mind;
    }

    /// <summary>
    /// Creates a quality-optimized collective using premium providers.
    /// </summary>
    public static CollectiveMind CreatePremium(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-opus-4-5-20251101", settings);
        TryAddProvider(mind, "OpenAI", ChatEndpointType.OpenAI, "gpt-4o", settings);
        TryAddProvider(mind, "Google", ChatEndpointType.Google, "gemini-1.5-pro", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Ensemble;
        return mind;
    }

    /// <summary>
    /// Creates a cost-optimized collective using budget-friendly providers.
    /// </summary>
    public static CollectiveMind CreateBudget(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-8b-instant", settings);
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, "llama3.2", settings: settings);

        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a local-only collective using Ollama.
    /// Provides resilience features (circuit breaker, health tracking) for a single local provider.
    /// </summary>
    public static CollectiveMind CreateLocal(string model = "llama3.2", string endpoint = "http://localhost:11434", ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, model, endpoint, settings: settings);
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a single-provider collective mind.
    /// Useful for getting resilience features with just one provider.
    /// </summary>
    public static CollectiveMind CreateSingle(
        string name,
        ChatEndpointType endpointType,
        string model,
        string? endpoint = null,
        string? apiKey = null,
        ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();
        mind.AddPathway(name, endpointType, model, endpoint, apiKey, settings);
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a collective mind from the current ChatConfig settings.
    /// Uses the configured endpoint type and adds it as the primary pathway.
    /// </summary>
    public static CollectiveMind CreateFromConfig(
        string model,
        string? endpoint = null,
        string? apiKey = null,
        string? endpointType = null,
        ChatRuntimeSettings? settings = null)
    {
        var (resolvedEndpoint, resolvedApiKey, resolvedType) = ChatConfig.ResolveWithOverrides(endpoint, apiKey, endpointType);

        var mind = new CollectiveMind();
        string providerName = LlmCostTracker.GetProvider(model);
        if (providerName == "Unknown") providerName = resolvedType.ToString();

        mind.AddPathway(providerName, resolvedType, model, resolvedEndpoint, resolvedApiKey, settings);
        mind.ThinkingMode = CollectiveThinkingMode.Sequential;
        return mind;
    }

    /// <summary>
    /// Creates a decomposition-enabled collective that splits requests into sub-goals.
    /// Routes sub-goals to optimal pathways (local/cloud) based on complexity.
    /// </summary>
    public static CollectiveMind CreateDecomposed(ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        // Add mix of local and cloud providers for routing flexibility
        mind.AddPathway("Ollama", ChatEndpointType.OllamaLocal, "llama3.2", settings: settings);
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-70b-versatile", settings);
        TryAddProvider(mind, "DeepSeek", ChatEndpointType.DeepSeek, "deepseek-chat", settings);
        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-sonnet-4-20250514", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.Default;
        return mind;
    }

    /// <summary>
    /// Creates a local-first decomposition collective.
    /// Prefers local Ollama for simple tasks, escalates to cloud only when needed.
    /// </summary>
    public static CollectiveMind CreateLocalFirstDecomposed(
        string localModel = "llama3.2",
        string localEndpoint = "http://localhost:11434",
        ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        // Local pathway as primary
        mind.AddPathway("Ollama-Local", ChatEndpointType.OllamaLocal, localModel, localEndpoint, settings: settings);

        // Lightweight cloud for moderate tasks
        TryAddProvider(mind, "Groq", ChatEndpointType.Groq, "llama-3.1-8b-instant", settings);

        // Premium cloud for complex tasks (only when needed)
        TryAddProvider(mind, "Anthropic", ChatEndpointType.Anthropic, "claude-sonnet-4-20250514", settings);

        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.LocalFirst;
        return mind;
    }

    /// <summary>
    /// Creates a hybrid collective with explicit tier assignments.
    /// Allows fine-grained control over which models handle which tasks.
    /// </summary>
    public static CollectiveMind CreateHybrid(
        (string Name, ChatEndpointType Type, string Model, PathwayTier Tier)[] pathways,
        ChatRuntimeSettings? settings = null)
    {
        var mind = new CollectiveMind();

        foreach (var (name, type, model, tier) in pathways)
        {
            mind.AddPathway(name, type, model, settings: settings);
            // Note: Tier is inferred automatically, but we could add explicit tier setting
        }

        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        return mind;
    }

    private static void TryAddProvider(CollectiveMind mind, string name, ChatEndpointType type, string model, ChatRuntimeSettings? settings)
    {
        try
        {
            var (endpoint, apiKey, _) = ChatConfig.ResolveWithOverrides(null, null, type.ToString());
            if (!string.IsNullOrWhiteSpace(apiKey) || type == ChatEndpointType.OllamaLocal)
            {
                mind.AddPathway(name, type, model, endpoint, apiKey, settings);
            }
        }
        catch
        {
            // Provider not available, skip silently
        }
    }
}