// ==========================================================
// Phase 2 Enhanced Orchestrator Builder
// Fluent builder for orchestrator with metacognitive components
// ==========================================================

using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Enhanced builder for creating orchestrator with Phase 2 metacognitive capabilities.
/// </summary>
public sealed class Phase2OrchestratorBuilder
{
    private Ouroboros.Abstractions.Core.IChatCompletionModel? _llm;
    private ToolRegistry? _tools;
    private IMemoryStore? _memory;
    private ISkillRegistry? _skills;
    private IUncertaintyRouter? _router;
    private ISafetyGuard? _safety;
    private IEthicsFramework? _ethics;
    private ISkillExtractor? _skillExtractor;
    private ICapabilityRegistry? _capabilityRegistry;
    private IGoalHierarchy? _goalHierarchy;
    private ISelfEvaluator? _selfEvaluator;
    private double _confidenceThreshold = 0.7;
    private SkillExtractionConfig? _skillConfig;
    private PersistentMemoryConfig? _memoryConfig;
    private CapabilityRegistryConfig? _capabilityConfig;
    private GoalHierarchyConfig? _goalConfig;
    private SelfEvaluatorConfig? _evaluatorConfig;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    public Phase2OrchestratorBuilder WithLLM(Ouroboros.Abstractions.Core.IChatCompletionModel llm)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
        return this;
    }

    /// <summary>
    /// Sets the tool registry.
    /// </summary>
    public Phase2OrchestratorBuilder WithTools(ToolRegistry tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools;
        return this;
    }

    /// <summary>
    /// Sets the memory store (uses PersistentMemoryStore by default).
    /// </summary>
    public Phase2OrchestratorBuilder WithMemory(IMemoryStore memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        return this;
    }

    /// <summary>
    /// Configures persistent memory with custom settings.
    /// </summary>
    public Phase2OrchestratorBuilder WithMemoryConfig(PersistentMemoryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _memoryConfig = config;
        return this;
    }

    /// <summary>
    /// Sets the skill registry.
    /// </summary>
    public Phase2OrchestratorBuilder WithSkills(ISkillRegistry skills)
    {
        ArgumentNullException.ThrowIfNull(skills);
        _skills = skills;
        return this;
    }

    /// <summary>
    /// Sets the skill extraction configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithSkillExtractionConfig(SkillExtractionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _skillConfig = config;
        return this;
    }

    /// <summary>
    /// Sets the capability registry configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithCapabilityConfig(CapabilityRegistryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _capabilityConfig = config;
        return this;
    }

    /// <summary>
    /// Sets the goal hierarchy configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithGoalConfig(GoalHierarchyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _goalConfig = config;
        return this;
    }

    /// <summary>
    /// Sets the self-evaluator configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithEvaluatorConfig(SelfEvaluatorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _evaluatorConfig = config;
        return this;
    }

    /// <summary>
    /// Sets the safety guard.
    /// </summary>
    public Phase2OrchestratorBuilder WithSafety(ISafetyGuard safety)
    {
        ArgumentNullException.ThrowIfNull(safety);
        _safety = safety;
        return this;
    }

    /// <summary>
    /// Sets the ethics framework.
    /// </summary>
    public Phase2OrchestratorBuilder WithEthics(IEthicsFramework ethics)
    {
        ArgumentNullException.ThrowIfNull(ethics);
        _ethics = ethics;
        return this;
    }

    /// <summary>
    /// Sets the uncertainty router confidence threshold.
    /// </summary>
    public Phase2OrchestratorBuilder WithConfidenceThreshold(double threshold)
    {
        _confidenceThreshold = Math.Clamp(threshold, 0.0, 1.0);
        return this;
    }

    /// <summary>
    /// Builds the orchestrator with all Phase 2 components.
    /// </summary>
    /// <returns>A tuple containing the orchestrator and all Phase 2 components</returns>
    public (
        IMetaAIPlannerOrchestrator Orchestrator,
        ICapabilityRegistry CapabilityRegistry,
        IGoalHierarchy GoalHierarchy,
        ISelfEvaluator SelfEvaluator) Build()
    {
        // Validate required components
        if (_llm == null)
            throw new InvalidOperationException("LLM must be set before building");
        if (_tools == null)
            throw new InvalidOperationException("Tools must be set before building");

        // Initialize defaults if not provided
        _safety ??= new SafetyGuard();
        _ethics ??= EthicsFrameworkFactory.CreateDefault();
        _skills ??= new SkillRegistry();
        _memory ??= new PersistentMemoryStore(config: _memoryConfig);

        // Create Phase 1 components
        _skillExtractor ??= new SkillExtractor(_llm, _skills, _ethics);
        _router ??= new UncertaintyRouter(null!, _confidenceThreshold);

        // Create Phase 2 components
        _capabilityRegistry ??= new CapabilityRegistry(_llm, _tools, _capabilityConfig);
        _goalHierarchy ??= new GoalHierarchy(_llm, _safety, _ethics, _goalConfig);

        // Create orchestrator
        MetaAIPlannerOrchestrator orchestrator = new MetaAIPlannerOrchestrator(
            _llm,
            _tools,
            _memory,
            _skills,
            _router,
            _safety,
            _ethics,
            approvalProvider: null,
            _skillExtractor);

        // Create self-evaluator (requires orchestrator)
        _selfEvaluator ??= new SelfEvaluator(
            _llm,
            _capabilityRegistry,
            _skills,
            _memory,
            orchestrator,
            _evaluatorConfig);

        return (orchestrator, _capabilityRegistry, _goalHierarchy, _selfEvaluator);
    }

    /// <summary>
    /// Creates a default Phase 2 orchestrator setup.
    /// </summary>
    public static (
        IMetaAIPlannerOrchestrator Orchestrator,
        ICapabilityRegistry CapabilityRegistry,
        IGoalHierarchy GoalHierarchy,
        ISelfEvaluator SelfEvaluator) CreateDefault(Ouroboros.Abstractions.Core.IChatCompletionModel llm)
    {
        ToolRegistry tools = ToolRegistry.CreateDefault();

        return new Phase2OrchestratorBuilder()
            .WithLLM(llm)
            .WithTools(tools)
            .Build();
    }
}
