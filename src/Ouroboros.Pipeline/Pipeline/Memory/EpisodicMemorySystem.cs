using Qdrant.Client;

namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Pre-configured episodic memory system with arrow factories.
/// </summary>
public sealed class EpisodicMemorySystem
{
    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly string _collectionName;

    internal EpisodicMemorySystem(QdrantClient qdrantClient, IEmbeddingModel embeddingModel, string collectionName)
    {
        _qdrantClient = qdrantClient;
        _embeddingModel = embeddingModel;
        _collectionName = collectionName;
    }

    /// <summary>
    /// Creates an arrow to store an episode.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> StoreEpisode(
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata)
        => EpisodicMemoryArrows.StoreEpisodeArrow(
            _qdrantClient,
            _embeddingModel,
            context,
            result,
            metadata,
            _collectionName);

    /// <summary>
    /// Creates an arrow to retrieve similar episodes.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> RetrieveSimilarEpisodes(
        string query,
        int topK = 5,
        double minSimilarity = 0.7)
        => EpisodicMemoryArrows.RetrieveSimilarEpisodesArrow(
            _qdrantClient,
            _embeddingModel,
            query,
            topK,
            minSimilarity,
            _collectionName);

    /// <summary>
    /// Creates an arrow to plan with experience.
    /// </summary>
    public Step<PipelineBranch, (PipelineBranch, Verification.Plan?)> PlanWithExperience(
        string goal,
        int topK = 5)
        => EpisodicMemoryArrows.PlanWithExperienceArrow(
            _qdrantClient,
            _embeddingModel,
            goal,
            topK,
            _collectionName);
}