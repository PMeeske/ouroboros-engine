using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OrchestrationCacheExtensionsTests
{
    #region WithCaching

    [Fact]
    public void WithCaching_ValidArgs_ShouldReturnCachingOrchestrator()
    {
        var mockOrchestrator = new Mock<IModelOrchestrator>();
        var cache = new InMemoryOrchestrationCache();

        var result = mockOrchestrator.Object.WithCaching(cache);

        result.Should().NotBeNull();
        cache.Dispose();
    }

    [Fact]
    public void WithCaching_WithCustomTtl_ShouldSetTtl()
    {
        var mockOrchestrator = new Mock<IModelOrchestrator>();
        var cache = new InMemoryOrchestrationCache();
        var customTtl = TimeSpan.FromMinutes(10);

        var result = mockOrchestrator.Object.WithCaching(cache, customTtl);

        result.Should().NotBeNull();
        cache.Dispose();
    }

    [Fact]
    public void WithCaching_DefaultTtl_ShouldBeFiveMinutes()
    {
        OrchestrationCacheExtensions.DefaultTtl.Should().Be(TimeSpan.FromMinutes(5));
    }

    #endregion
}
