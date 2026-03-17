using FluentAssertions;
using Ouroboros.Providers.Configuration;
using CoreDefaults = Ouroboros.Core.Configuration.DefaultEndpoints;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public sealed class DefaultEndpointsTests2
{
    [Fact]
    public void Ollama_MatchesCoreDefaults()
    {
        DefaultEndpoints.Ollama.Should().Be(CoreDefaults.Ollama);
    }

    [Fact]
    public void Qdrant_MatchesCoreDefaults()
    {
        DefaultEndpoints.Qdrant.Should().Be(CoreDefaults.Qdrant);
    }

    [Fact]
    public void QdrantGrpc_MatchesCoreDefaults()
    {
        DefaultEndpoints.QdrantGrpc.Should().Be(CoreDefaults.QdrantGrpc);
    }

    [Fact]
    public void Ollama_IsValidUrl()
    {
        var result = Uri.TryCreate(DefaultEndpoints.Ollama, UriKind.Absolute, out var uri);
        result.Should().BeTrue();
        uri!.Port.Should().Be(11434);
    }

    [Fact]
    public void Qdrant_IsValidUrl()
    {
        var result = Uri.TryCreate(DefaultEndpoints.Qdrant, UriKind.Absolute, out var uri);
        result.Should().BeTrue();
        uri!.Port.Should().Be(6333);
    }

    [Fact]
    public void QdrantGrpc_IsValidUrl()
    {
        var result = Uri.TryCreate(DefaultEndpoints.QdrantGrpc, UriKind.Absolute, out var uri);
        result.Should().BeTrue();
        uri!.Port.Should().Be(6334);
    }

    [Fact]
    public void AllEndpoints_AreNotNullOrEmpty()
    {
        DefaultEndpoints.Ollama.Should().NotBeNullOrWhiteSpace();
        DefaultEndpoints.Qdrant.Should().NotBeNullOrWhiteSpace();
        DefaultEndpoints.QdrantGrpc.Should().NotBeNullOrWhiteSpace();
    }
}
