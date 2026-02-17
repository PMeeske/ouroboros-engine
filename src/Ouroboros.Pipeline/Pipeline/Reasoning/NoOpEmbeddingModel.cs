namespace Ouroboros.Pipeline.Reasoning;

/// <summary>
/// A no-op embedding model used when actual embedding is not required.
/// </summary>
internal sealed class NoOpEmbeddingModel : IEmbeddingModel
{
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<float>());
}