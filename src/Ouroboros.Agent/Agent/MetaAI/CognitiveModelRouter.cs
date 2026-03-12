// <copyright file="CognitiveModelRouter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Routes cognitive tasks to specialized ML models with a fallback chain.
/// Each <see cref="CognitiveTask"/> maps to a prioritized list of model routes
/// across providers: HuggingFace (specialized classifiers) -> Ollama cloud
/// (reasoning-heavy) -> Ollama local -> general LLM fallback.
///
/// Default routes are pre-configured for common cognitive tasks using
/// well-known open models. Custom routes can be registered at runtime.
/// </summary>
public sealed class CognitiveModelRouter
{
    /// <summary>Cognitive tasks that can be routed to specialized models.</summary>
    public enum CognitiveTask
    {
        /// <summary>Emotion detection from text (e.g., GoEmotions).</summary>
        EmotionDetection,

        /// <summary>Sentiment analysis (positive/negative/neutral).</summary>
        SentimentAnalysis,

        /// <summary>Moral reasoning and ethical judgment.</summary>
        MoralReasoning,

        /// <summary>Bias detection in text.</summary>
        BiasDetection,

        /// <summary>Narrative coherence scoring.</summary>
        NarrativeCoherence,

        /// <summary>Creativity and novelty scoring.</summary>
        CreativityScoring,

        /// <summary>Social cognition and theory of mind tasks.</summary>
        SocialCognition,

        /// <summary>Habit and behavioral pattern matching.</summary>
        HabitPatternMatching,

        /// <summary>Counterfactual simulation and reasoning.</summary>
        CounterfactualSim,

        /// <summary>Predictive processing and surprise minimization.</summary>
        PredictiveProcessing,

        /// <summary>Attention priority and salience routing.</summary>
        AttentionPriority,

        /// <summary>Aesthetic scoring for visual/textual content.</summary>
        AestheticScoring,

        /// <summary>Face and expression emotion recognition.</summary>
        FaceEmotionRecognition,

        /// <summary>General-purpose reasoning (LLM fallback).</summary>
        GeneralReasoning,
    }

    /// <summary>A route mapping a cognitive task to a specific model and provider.</summary>
    /// <param name="Task">The cognitive task this route handles.</param>
    /// <param name="ModelId">Model identifier (HF repo ID, Ollama model name, etc.).</param>
    /// <param name="Provider">Provider type: "huggingface", "ollama-cloud", "ollama-local", "llm".</param>
    /// <param name="Endpoint">API endpoint URL for this provider.</param>
    /// <param name="ExpectedLatencyMs">Expected latency in milliseconds.</param>
    public sealed record ModelRoute(
        CognitiveTask Task,
        string ModelId,
        string Provider,
        string Endpoint,
        double ExpectedLatencyMs);

    private readonly Dictionary<CognitiveTask, List<ModelRoute>> _routes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveModelRouter"/> class
    /// with default routes for all supported cognitive tasks.
    /// </summary>
    public CognitiveModelRouter()
    {
        // HuggingFace specialized models (low latency, task-specific)
        RegisterRoute(CognitiveTask.EmotionDetection,
            "SamLowe/roberta-base-go_emotions", "huggingface");
        RegisterRoute(CognitiveTask.SentimentAnalysis,
            "cardiffnlp/twitter-roberta-base-sentiment-latest", "huggingface");
        RegisterRoute(CognitiveTask.MoralReasoning,
            "facebook/bart-large-mnli", "huggingface");
        RegisterRoute(CognitiveTask.BiasDetection,
            "d4data/bias-detection-model", "huggingface");
        RegisterRoute(CognitiveTask.NarrativeCoherence,
            "sentence-transformers/all-mpnet-base-v2", "huggingface");
        RegisterRoute(CognitiveTask.CreativityScoring,
            "sentence-transformers/all-MiniLM-L6-v2", "huggingface");
        RegisterRoute(CognitiveTask.SocialCognition,
            "facebook/bart-large-mnli", "huggingface");
        RegisterRoute(CognitiveTask.AestheticScoring,
            "openai/clip-vit-base-patch32", "huggingface");

        // Ollama cloud routes for reasoning-heavy tasks
        RegisterRoute(CognitiveTask.CounterfactualSim,
            "deepseek-r1:8b", "ollama-cloud");
        RegisterRoute(CognitiveTask.PredictiveProcessing,
            "deepseek-r1:8b", "ollama-cloud");
        RegisterRoute(CognitiveTask.HabitPatternMatching,
            "all-minilm:latest", "ollama-cloud");
        RegisterRoute(CognitiveTask.AttentionPriority,
            "nomic-embed-text:latest", "ollama-cloud");
        RegisterRoute(CognitiveTask.FaceEmotionRecognition,
            "llava:13b", "ollama-cloud");
    }

    /// <summary>
    /// Registers a model route for a cognitive task. Multiple routes per task
    /// form a fallback chain in registration order.
    /// </summary>
    /// <param name="task">The cognitive task to route.</param>
    /// <param name="modelId">Model identifier.</param>
    /// <param name="provider">Provider type.</param>
    /// <param name="endpoint">Optional custom endpoint (defaults per provider).</param>
    public void RegisterRoute(
        CognitiveTask task, string modelId, string provider, string? endpoint = null)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(provider);

        var route = new ModelRoute(
            task,
            modelId,
            provider,
            endpoint ?? GetDefaultEndpoint(provider),
            EstimateLatency(provider));

        if (!_routes.TryGetValue(task, out var routes))
        {
            routes = [];
            _routes[task] = routes;
        }

        routes.Add(route);
    }

    /// <summary>
    /// Gets the primary (best) route for a cognitive task.
    /// Falls back to a generic LLM route if no specific route is registered.
    /// </summary>
    /// <param name="task">The cognitive task to route.</param>
    /// <returns>The best available model route.</returns>
    public ModelRoute GetRoute(CognitiveTask task)
    {
        if (_routes.TryGetValue(task, out var routes) && routes.Count > 0)
        {
            return routes[0];
        }

        return new ModelRoute(task, "general", "llm", string.Empty, 2000);
    }

    /// <summary>
    /// Gets the full fallback chain for a cognitive task.
    /// Always ends with a generic LLM fallback route.
    /// </summary>
    /// <param name="task">The cognitive task to get fallback chain for.</param>
    /// <returns>Ordered list of routes from most to least preferred.</returns>
    public List<ModelRoute> GetFallbackChain(CognitiveTask task)
    {
        var chain = new List<ModelRoute>();

        if (_routes.TryGetValue(task, out var routes))
        {
            chain.AddRange(routes);
        }

        chain.Add(new ModelRoute(task, "general-llm", "llm", string.Empty, 3000));
        return chain;
    }

    /// <summary>
    /// Gets all registered primary routes keyed by cognitive task.
    /// </summary>
    /// <returns>Dictionary of cognitive tasks to their primary routes.</returns>
    public Dictionary<CognitiveTask, ModelRoute> GetAllRoutes()
        => _routes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[0]);

    /// <summary>
    /// Gets the default API endpoint for a provider type.
    /// </summary>
    private static string GetDefaultEndpoint(string provider) => provider switch
    {
        "huggingface" => "https://api-inference.huggingface.co/models/",
        "ollama-cloud" => "https://ollama.cloud/api/",
        "ollama-local" => "http://localhost:11434/",
        _ => string.Empty,
    };

    /// <summary>
    /// Estimates expected latency in milliseconds for a provider type.
    /// </summary>
    private static double EstimateLatency(string provider) => provider switch
    {
        "huggingface" => 500,
        "ollama-cloud" => 1000,
        "ollama-local" => 300,
        "llm" => 2000,
        _ => 3000,
    };
}
