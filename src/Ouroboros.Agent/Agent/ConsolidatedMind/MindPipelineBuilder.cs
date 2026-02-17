namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Fluent builder for constructing ConsolidatedMind pipelines.
/// </summary>
public sealed class MindPipelineBuilder
{
    private readonly ConsolidatedMind _mind;
    private readonly List<Step<PipelineBranch, PipelineBranch>> _steps = new();

    internal MindPipelineBuilder(ConsolidatedMind mind)
    {
        _mind = mind;
    }

    /// <summary>
    /// Adds intelligent reasoning step.
    /// </summary>
    public MindPipelineBuilder WithReasoning(IEmbeddingModel embed, string topic, string query, int k = 8)
    {
        _steps.Add(ConsolidatedMindArrows.IntelligentReasoningArrow(_mind, embed, topic, query, k));
        return this;
    }

    /// <summary>
    /// Adds draft step.
    /// </summary>
    public MindPipelineBuilder WithDraft(IEmbeddingModel embed, string topic, string query, int k = 8)
    {
        _steps.Add(ConsolidatedMindArrows.SmartDraftArrow(_mind, embed, topic, query, k));
        return this;
    }

    /// <summary>
    /// Adds critique step.
    /// </summary>
    public MindPipelineBuilder WithCritique(IEmbeddingModel embed, string topic, string query, int k = 8)
    {
        _steps.Add(ConsolidatedMindArrows.SmartCritiqueArrow(_mind, embed, topic, query, k));
        return this;
    }

    /// <summary>
    /// Adds verification step.
    /// </summary>
    public MindPipelineBuilder WithVerification()
    {
        _steps.Add(ConsolidatedMindArrows.VerificationArrow(_mind));
        return this;
    }

    /// <summary>
    /// Adds a custom step.
    /// </summary>
    public MindPipelineBuilder WithStep(Step<PipelineBranch, PipelineBranch> step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Builds the composed pipeline.
    /// </summary>
    /// <returns>A single composed step.</returns>
    public Step<PipelineBranch, PipelineBranch> Build()
    {
        if (_steps.Count == 0)
        {
            return branch => Task.FromResult(branch);
        }

        return async branch =>
        {
            var current = branch;
            foreach (var step in _steps)
            {
                current = await step(current);
            }
            return current;
        };
    }
}