namespace Ouroboros.Providers;

/// <summary>
/// Builder for creating RoundRobinChatModel instances with fluent API.
/// </summary>
public sealed class RoundRobinBuilder
{
    private readonly List<ProviderConfig> _configs = new();
    private readonly ChatRuntimeSettings? _settings;
    private bool _failoverEnabled = true;
    private int _maxRetries = 3;

    public RoundRobinBuilder(ChatRuntimeSettings? settings = null)
    {
        _settings = settings;
    }

    /// <summary>
    /// Adds a provider to the pool.
    /// </summary>
    public RoundRobinBuilder AddProvider(
        string name,
        ChatEndpointType endpointType,
        string? model = null,
        string? endpoint = null,
        string? apiKey = null,
        int weight = 1)
    {
        _configs.Add(new ProviderConfig(name, endpointType, endpoint, apiKey, model, weight));
        return this;
    }

    /// <summary>
    /// Adds Anthropic Claude to the pool.
    /// </summary>
    public RoundRobinBuilder AddAnthropic(string model = "claude-sonnet-4-20250514", string? apiKey = null)
        => AddProvider("Anthropic", ChatEndpointType.Anthropic, model, apiKey: apiKey);

    /// <summary>
    /// Adds OpenAI to the pool.
    /// </summary>
    public RoundRobinBuilder AddOpenAI(string model = "gpt-4o", string? apiKey = null)
        => AddProvider("OpenAI", ChatEndpointType.OpenAI, model, apiKey: apiKey);

    /// <summary>
    /// Adds DeepSeek to the pool.
    /// </summary>
    public RoundRobinBuilder AddDeepSeek(string model = "deepseek-chat", string? apiKey = null)
        => AddProvider("DeepSeek", ChatEndpointType.DeepSeek, model, apiKey: apiKey);

    /// <summary>
    /// Adds Groq to the pool.
    /// </summary>
    public RoundRobinBuilder AddGroq(string model = "llama-3.1-70b-versatile", string? apiKey = null)
        => AddProvider("Groq", ChatEndpointType.Groq, model, apiKey: apiKey);

    /// <summary>
    /// Adds local Ollama to the pool.
    /// </summary>
    public RoundRobinBuilder AddOllama(string model = "llama3.2", string endpoint = "http://localhost:11434")
        => AddProvider("Ollama", ChatEndpointType.OllamaLocal, model, endpoint);

    /// <summary>
    /// Adds Google Gemini to the pool.
    /// </summary>
    public RoundRobinBuilder AddGoogle(string model = "gemini-2.0-flash", string? apiKey = null)
        => AddProvider("Google", ChatEndpointType.Google, model, apiKey: apiKey);

    /// <summary>
    /// Adds Mistral AI to the pool.
    /// </summary>
    public RoundRobinBuilder AddMistral(string model = "mistral-large", string? apiKey = null)
        => AddProvider("Mistral", ChatEndpointType.Mistral, model, apiKey: apiKey);

    /// <summary>
    /// Enables or disables automatic failover.
    /// </summary>
    public RoundRobinBuilder WithFailover(bool enabled = true)
    {
        _failoverEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    public RoundRobinBuilder WithMaxRetries(int retries)
    {
        _maxRetries = retries;
        return this;
    }

    /// <summary>
    /// Builds the RoundRobinChatModel.
    /// </summary>
    public RoundRobinChatModel Build()
    {
        var model = new RoundRobinChatModel(_failoverEnabled, _maxRetries);

        foreach (var config in _configs)
        {
            model.AddProvider(config, _settings);
        }

        return model;
    }
}