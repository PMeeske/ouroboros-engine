namespace Ouroboros.Tests.Pipeline.Ingestion;

using Ouroboros.Pipeline.Ingestion;

[Trait("Category", "Unit")]
public class DirectoryIngestionOptionsTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var opts = new DirectoryIngestionOptions();

        opts.Recursive.Should().BeTrue();
        opts.Patterns.Should().BeEmpty();
        opts.Extensions.Should().BeNull();
        opts.ExcludeDirectories.Should().BeNull();
        opts.MaxFileBytes.Should().Be(0);
        opts.DisableCache.Should().BeFalse();
        opts.CacheFilePath.Should().Be(".monadic_ingest_cache.json");
        opts.ChunkSize.Should().Be(2000);
        opts.ChunkOverlap.Should().Be(200);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var opts = new DirectoryIngestionOptions
        {
            Recursive = false,
            Patterns = new[] { "*.md" },
            Extensions = new[] { ".cs", ".ts" },
            ExcludeDirectories = new[] { "bin", "obj" },
            MaxFileBytes = 1024 * 1024,
            DisableCache = true,
            CacheFilePath = "custom_cache.json",
            ChunkSize = 500,
            ChunkOverlap = 50,
        };

        opts.Recursive.Should().BeFalse();
        opts.Patterns.Should().Contain("*.md");
        opts.Extensions.Should().Contain(".cs");
        opts.ExcludeDirectories.Should().Contain("bin");
        opts.MaxFileBytes.Should().Be(1024 * 1024);
        opts.DisableCache.Should().BeTrue();
        opts.CacheFilePath.Should().Be("custom_cache.json");
        opts.ChunkSize.Should().Be(500);
        opts.ChunkOverlap.Should().Be(50);
    }
}
