using Ouroboros.Providers.Configuration;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class DefaultEndpointsTests
{
    [Fact]
    public void Ollama_IsLocalhost11434()
    {
        DefaultEndpoints.Ollama.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void Qdrant_IsLocalhost6333()
    {
        DefaultEndpoints.Qdrant.Should().Be("http://localhost:6333");
    }

    [Fact]
    public void QdrantGrpc_IsLocalhost6334()
    {
        DefaultEndpoints.QdrantGrpc.Should().Be("http://localhost:6334");
    }

    [Fact]
    public void Ollama_ContainsValidUri()
    {
        Uri.TryCreate(DefaultEndpoints.Ollama, UriKind.Absolute, out _).Should().BeTrue();
    }

    [Fact]
    public void Qdrant_ContainsValidUri()
    {
        Uri.TryCreate(DefaultEndpoints.Qdrant, UriKind.Absolute, out _).Should().BeTrue();
    }

    [Fact]
    public void QdrantGrpc_ContainsValidUri()
    {
        Uri.TryCreate(DefaultEndpoints.QdrantGrpc, UriKind.Absolute, out _).Should().BeTrue();
    }
}
