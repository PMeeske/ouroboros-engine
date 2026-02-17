namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Pre-configured ConsolidatedMind arrow system with explicit dependencies.
/// This class demonstrates the arrow parameterization pattern where configuration
/// is explicit and arrows are created on-demand rather than holding state.
/// </summary>
public sealed class ConfiguredMindArrowSystem
{
    private readonly IEnumerable<SpecializedModel> _specialists;
    private readonly MindConfig _config;

    internal ConfiguredMindArrowSystem(IEnumerable<SpecializedModel> specialists, MindConfig config)
    {
        _specialists = specialists;
        _config = config;
    }

    /// <summary>
    /// Creates a reasoning arrow for the given topic and query.
    /// Dependencies are passed explicitly, not stored as instance state.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> CreateReasoningArrow(
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
        => ConsolidatedMindArrowsExtensions.ReasoningArrowWithExplicitConfig(
            _specialists,
            _config,
            embed,
            topic,
            query,
            k);

    /// <summary>
    /// Creates a complex task arrow with the configured specialists and settings.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> CreateComplexTaskArrow(
        IEmbeddingModel embed,
        string task,
        int k = 8)
        => ConsolidatedMindArrowsExtensions.ComplexTaskArrowWithExplicitConfig(
            _specialists,
            _config,
            embed,
            task,
            k);

    /// <summary>
    /// Creates a processing arrow factory for custom prompts.
    /// </summary>
    public Func<string, Step<PipelineBranch, PipelineBranch>> CreateProcessingFactory(ToolRegistry? tools = null)
        => ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(_specialists, _config, tools);

    /// <summary>
    /// Gets the configuration used by this system.
    /// </summary>
    public MindConfig Configuration => _config;

    /// <summary>
    /// Gets the specialists available in this system.
    /// </summary>
    public IEnumerable<SpecializedModel> Specialists => _specialists;
}