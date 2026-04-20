using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OrchestrationCacheExtensionsTests
{
    [Fact]
    public void DefaultTtl_IsFiveMinutes()
    {
        OrchestrationCacheExtensions.DefaultTtl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void WithCaching_ValidArgs_ReturnsCachingOrchestrator()
    {
        var mockOrchestrator = new Mock<IModelOrchestrator>();
        var mockCache = new Mock<IOrchestrationCache>();

        var result = mockOrchestrator.Object.WithCaching(mockCache.Object);

        result.Should().NotBeNull();
        result.Should().BeOfType<CachingModelOrchestrator>();
    }

    [Fact]
    public void WithCaching_CustomTtl_ReturnsCachingOrchestrator()
    {
        var mockOrchestrator = new Mock<IModelOrchestrator>();
        var mockCache = new Mock<IOrchestrationCache>();
        var ttl = TimeSpan.FromMinutes(10);

        var result = mockOrchestrator.Object.WithCaching(mockCache.Object, ttl);

        result.Should().NotBeNull();
    }

    [Fact]
    public void WithCaching_NullTtl_UsesDefaultTtl()
    {
        var mockOrchestrator = new Mock<IModelOrchestrator>();
        var mockCache = new Mock<IOrchestrationCache>();

        var result = mockOrchestrator.Object.WithCaching(mockCache.Object, null);

        result.Should().NotBeNull();
    }
}
