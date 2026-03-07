namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class QdrantDagConfigTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
#pragma warning disable CS0618 // Obsolete
        var config = new QdrantDagConfig();
#pragma warning restore CS0618

        config.Endpoint.Should().Be("http://localhost:6334");
        config.NodesCollection.Should().Be("ouroboros_dag_nodes");
        config.EdgesCollection.Should().Be("ouroboros_dag_edges");
        config.VectorSize.Should().Be(1536);
        config.UseHttps.Should().BeFalse();
    }

    [Fact]
    public void CustomValues_ArePreserved()
    {
#pragma warning disable CS0618
        var config = new QdrantDagConfig(
            Endpoint: "https://qdrant.cloud:6334",
            NodesCollection: "custom_nodes",
            EdgesCollection: "custom_edges",
            VectorSize: 768,
            UseHttps: true);
#pragma warning restore CS0618

        config.Endpoint.Should().Be("https://qdrant.cloud:6334");
        config.VectorSize.Should().Be(768);
        config.UseHttps.Should().BeTrue();
    }
}
